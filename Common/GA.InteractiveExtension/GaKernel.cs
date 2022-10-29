namespace GA.InteractiveExtension;

using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Commands;

public class GaKernel : Kernel, IKernelCommandHandler<SubmitCode>
{
    public GaKernel() : base("ga")
    {
    }

    public Task HandleAsync(SubmitCode command, KernelInvocationContext context)
    {
        object obj = "<div>Hello from Guitar Alchemist</div>";
        context.Display(obj);
        return Task.CompletedTask;
    }
}