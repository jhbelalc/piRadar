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
}
