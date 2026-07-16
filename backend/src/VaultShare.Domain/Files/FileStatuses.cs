namespace VaultShare.Domain.Files;

public enum UploadStatus { Pending, Uploading, Uploaded, Processing, Completed, Failed, Abandoned }
public enum MalwareScanStatus { Pending, Scanning, Clean, Infected, ScannerUnavailable, Failed }
public enum EncryptionStatus { Pending, Encrypting, Encrypted, Failed }
public enum AvailabilityStatus { Processing, Available, Quarantined, Deleted, Purged, Failed }
