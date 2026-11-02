using PiRadar.Core.Abstractions;
using PiRadar.Core.Domain;
using PiRadar.Data.Records;

namespace PiRadar.Data.Repositories;

internal sealed class ReceiptRepository(DatabaseHost host) : RepositoryBase(host), IReceiptRepository
{
    public async Task<Receipt?> GetAsync(Guid id) =>
        (await Connection.FindAsync<ReceiptRecord>(id))?.ToDomain();

    public async Task<ReceiptImage?> GetImageAsync(Guid receiptId) =>
        (await Connection.FindAsync<ReceiptImageRecord>(receiptId))?.ToDomain();

    public Task ReplaceImageAsync(Guid receiptId, ReceiptImage image) => InTransactionAsync(db =>
    {
        if (db.Find<ReceiptImageRecord>(receiptId) is null)
        {
            throw new KeyNotFoundException(AppLocale.Pick("El recibo no existe.", "The receipt does not exist."));
        }

        image.ReceiptId = receiptId;
        return db.Update(image.ToRecord());
    });
}
