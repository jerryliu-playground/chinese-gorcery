namespace ChineseGrocery.Sim.Core;

public enum RejectReason
{
    WrongPhase,
    UnknownProduct,
    NotOrderable,
    NoStorageForProduct,
    InvalidQuantity,
    InsufficientCash,
    UnknownFixture,
    FixtureLocked,
    UnknownFixtureInstance,
    OutOfBounds,
    Overlap,
    PathBlocked,
    MissingEntrance,
    LastEntrance,
    FixtureNotEmpty,
    StorageNeededForOrders,
}
