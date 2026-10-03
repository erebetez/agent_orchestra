namespace AgentOrchestra.App.Domain;

public enum State : short
{
    All = -100,
    Invalid = -1,
    Inactive = 0,
    Active = 1,

    Requested = 5,
    HasError = 8,
    Created = 10,
    Prepared = 12,
    InProgress = 15,
    Measured = 18,
    Resolved = 19,
    Done = 20,
    Validated = 25,
    Transferring = 28,
    Transferred = 30,
    Canceled = 40
}
