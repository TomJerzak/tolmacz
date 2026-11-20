using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Tolmacz;

/// <summary>Runs the merged Marian decoder for one sentence and keeps its cache between steps.</summary>
internal sealed class OnnxBeamDecoder : IBeamDecoder
{
    private const string PastPrefix = "past_key_values.";
    private const string PresentPrefix = "present.";

    private readonly InferenceSession _session;
    private readonly string[] _pastNames;
    private readonly long[] _attentionMask;
    private readonly float[] _encoderState;
    private readonly int _heads;
    private readonly int _headSize;
    private readonly Dictionary<string, float[]> _cache = [];

    private int _batchSize = 1;
    private bool _hasCache;

    /// <summary>Prepares the decoder for one encoded sentence.</summary>
    public OnnxBeamDecoder(InferenceSession session, long[] attentionMask, float[] encoderState)
    {
        _session = session;
        _attentionMask = attentionMask;
        _encoderState = encoderState;
        _pastNames = session.InputMetadata.Keys
            .Where(name => name.StartsWith(PastPrefix, StringComparison.Ordinal))
            .ToArray();

        // Cache tensors have the shape [batch, heads, sequence, head size].
        var cacheShape = session.InputMetadata[_pastNames[0]].Dimensions;
        _heads = cacheShape[1];
        _headSize = cacheShape[3];
    }

    /// <inheritdoc />
    public float[] Step(IReadOnlyList<long> lastTokens)
    {
        _batchSize = lastTokens.Count;
        var inputs = new Dictionary<string, OrtValue>();

        try
        {
            AddInputs(inputs, lastTokens);
            var presentNames = GetRequestedCacheNames();

            using var runOptions = new RunOptions();
            using var outputs = _session.Run(runOptions, inputs, presentNames.Prepend("logits").ToArray());

            for (var i = 0; i < presentNames.Length; i++)
            {
                _cache[presentNames[i]] = outputs[i + 1].GetTensorDataAsSpan<float>().ToArray();
            }

            _hasCache = true;
            return outputs[0].GetTensorDataAsSpan<float>().ToArray();
        }
        finally
        {
            foreach (var input in inputs.Values)
            {
                input.Dispose();
            }
        }
    }

    /// <inheritdoc />
    public void Reorder(IReadOnlyList<int> parents)
    {
        if (parents.Count == _batchSize && parents.Select((parent, index) => parent == index).All(same => same))
        {
            return;
        }

        foreach (var name in _cache.Keys.ToArray())
        {
            var source = _cache[name];
            var rowLength = source.Length / _batchSize;
            var target = new float[rowLength * parents.Count];
            for (var i = 0; i < parents.Count; i++)
            {
                Array.Copy(source, parents[i] * rowLength, target, i * rowLength, rowLength);
            }

            _cache[name] = target;
        }

        _batchSize = parents.Count;
    }

    private void AddInputs(Dictionary<string, OrtValue> inputs, IReadOnlyList<long> lastTokens)
    {
        var sourceLength = _attentionMask.Length;
        var hiddenSize = _encoderState.Length / sourceLength;

        inputs["input_ids"] = CreateTensor(lastTokens.ToArray(), [_batchSize, 1]);
        inputs["encoder_attention_mask"] = CreateTensor(Repeat(_attentionMask, _batchSize), [_batchSize, sourceLength]);
        inputs["encoder_hidden_states"] = CreateTensor(Repeat(_encoderState, _batchSize), [_batchSize, sourceLength, hiddenSize]);
        inputs["use_cache_branch"] = CreateTensor(new[] { _hasCache }, [1]);
        foreach (var name in _pastNames)
        {
            inputs[name] = CreatePastTensor(name);
        }
    }

    // After the first step the encoder half of the cache never changes.
    private string[] GetRequestedCacheNames() => _pastNames
        .Select(ToPresentName)
        .Where(name => !_hasCache || name.Contains(".decoder.", StringComparison.Ordinal))
        .ToArray();

    private OrtValue CreatePastTensor(string pastName)
    {
        if (!_hasCache)
        {
            return OrtValue.CreateAllocatedTensorValue(
                OrtAllocator.DefaultInstance,
                TensorElementType.Float,
                [_batchSize, _heads, 0, _headSize]);
        }

        var data = _cache[ToPresentName(pastName)];
        var sequenceLength = data.Length / (_batchSize * _heads * _headSize);
        return CreateTensor(data, [_batchSize, _heads, sequenceLength, _headSize]);
    }

    private static T[] Repeat<T>(T[] row, int count)
    {
        if (count == 1)
        {
            return row;
        }

        var result = new T[row.Length * count];
        for (var i = 0; i < count; i++)
        {
            Array.Copy(row, 0, result, i * row.Length, row.Length);
        }

        return result;
    }

    private static OrtValue CreateTensor<T>(T[] data, long[] shape) where T : unmanaged =>
        OrtValue.CreateTensorValueFromMemory(OrtMemoryInfo.DefaultInstance, data.AsMemory(), shape);

    private static string ToPresentName(string pastName) =>
        PresentPrefix + pastName[PastPrefix.Length..];
}
