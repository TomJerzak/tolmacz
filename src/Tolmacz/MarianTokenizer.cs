using System.Text;
using System.Text.Json;
using Microsoft.ML.Tokenizers;

namespace Tolmacz;

/// <summary>
/// Converts text to and from model token ids. Pieces come from SentencePiece, but ids
/// come from the Marian vocabulary, which is the numbering the model was trained with.
/// </summary>
internal sealed class MarianTokenizer
{
    private const string WordBoundary = "▁";

    private readonly SentencePieceTokenizer _sentencePiece;
    private readonly Dictionary<string, int> _idsByPiece;
    private readonly string[] _piecesById;

    /// <summary>Loads the tokenizer files from the model folder.</summary>
    public MarianTokenizer(string modelDirectory)
    {
        using (var model = File.OpenRead(Path.Combine(modelDirectory, "source.spm")))
        {
            // The two flags turn off automatic begin- and end-of-sentence tokens.
            _sentencePiece = SentencePieceTokenizer.Create(model, false, false);
        }

        using (var vocabulary = File.OpenRead(Path.Combine(modelDirectory, "vocab.json")))
        {
            _idsByPiece = JsonSerializer.Deserialize<Dictionary<string, int>>(vocabulary)
                ?? throw new InvalidDataException("vocab.json is empty.");
        }

        _piecesById = new string[_idsByPiece.Count];
        foreach (var (piece, id) in _idsByPiece)
        {
            _piecesById[id] = piece;
        }

        PadId = _idsByPiece["<pad>"];
        EndOfSentenceId = _idsByPiece["</s>"];
        UnknownId = _idsByPiece["<unk>"];
        EnglishId = _idsByPiece[">>eng<<"];
        PolishId = _idsByPiece[">>pol<<"];
    }

    /// <summary>The number of tokens the model knows.</summary>
    public int VocabularySize => _piecesById.Length;
    /// <summary>The padding token, which also starts decoding.</summary>
    public int PadId { get; }
    /// <summary>The token that ends a sequence.</summary>
    public int EndOfSentenceId { get; }
    /// <summary>The token used for text outside the vocabulary.</summary>
    public int UnknownId { get; }
    /// <summary>The token that selects English as the target language.</summary>
    public int EnglishId { get; }
    /// <summary>The token that selects Polish as the target language.</summary>
    public int PolishId { get; }

    /// <summary>Returns whether any part of the text is missing from the model vocabulary.</summary>
    public bool ContainsUnknown(string text) =>
        Encode(text, TranslationDirection.EnglishToPolish).Contains(UnknownId);

    /// <summary>Converts text to model input: target language token, text tokens, end token.</summary>
    public long[] Encode(string text, TranslationDirection direction)
    {
        var tokens = _sentencePiece.EncodeToTokens(text, out _);
        var ids = new long[tokens.Count + 2];
        ids[0] = direction == TranslationDirection.EnglishToPolish ? PolishId : EnglishId;

        for (var i = 0; i < tokens.Count; i++)
        {
            ids[i + 1] = _idsByPiece.GetValueOrDefault(tokens[i].Value, UnknownId);
        }

        ids[^1] = EndOfSentenceId;
        return ids;
    }

    /// <summary>Converts generated tokens to text, skipping special tokens.</summary>
    public string Decode(IEnumerable<long> ids)
    {
        var text = new StringBuilder();
        foreach (var id in ids)
        {
            if (id != PadId && id != EndOfSentenceId && id != UnknownId
                && id != EnglishId && id != PolishId && id > 0)
            {
                text.Append(_piecesById[id]);
            }
        }

        return text.Replace(WordBoundary, " ").ToString().Trim();
    }
}
