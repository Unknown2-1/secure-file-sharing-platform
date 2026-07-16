namespace VaultShare.Domain.Shares;

public sealed class ShareItem
{
    private ShareItem() { }
    public ShareItem(Guid shareId, Guid storedFileId) { ShareId = shareId; StoredFileId = storedFileId; }
    public Guid ShareId { get; private set; }
    public Guid StoredFileId { get; private set; }
}
