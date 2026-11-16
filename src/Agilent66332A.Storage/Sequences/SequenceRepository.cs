using Agilent66332A.Core.Sequences;
using Agilent66332A.Storage.Preferences;

namespace Agilent66332A.Storage.Sequences;

public static class SequenceRepository
{
    public static async Task<SequenceDocument> LoadAsync(string path, CancellationToken token = default)
    {
        var document = await JsonFiles.ReadAsync<SequenceDocument>(path, token: token).ConfigureAwait(false);
        Validate(document);
        return document;
    }
    public static Task SaveAsync(string path, SequenceDocument document, CancellationToken token = default)
    {
        Validate(document);
        return JsonFiles.WriteAtomicAsync(path, document, token);
    }
    private static void Validate(SequenceDocument document)
    {
        var errors = SequenceValidator.Validate(document);
        if (errors.Count != 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
    }
}
