using TableSnap.Models;

namespace TableSnap.Recognition;

public interface ITableRecognizer
{
    Task<TableDocument> RecognizeAsync(byte[] pngBytes, CancellationToken cancellationToken = default);
}
