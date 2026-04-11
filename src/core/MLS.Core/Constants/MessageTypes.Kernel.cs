namespace MLS.Core.Constants;

public static partial class MessageTypes
{
    /// <summary>Kernel initialization completed and kernel entered a ready state.</summary>
    public const string KernelInitialized = "KERNEL_INITIALIZED";

    /// <summary>Kernel entered a faulted state during initialization or execution.</summary>
    public const string KernelFaulted = "KERNEL_FAULTED";

    /// <summary>Kernel resources were disposed and kernel became invalid for execution.</summary>
    public const string KernelDisposed = "KERNEL_DISPOSED";
}
