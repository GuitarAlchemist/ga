namespace GA.InteractiveExtension.Kernels;

using Microsoft.DotNet.Interactive.Commands;

public class GaKernel() : Kernel("ga"), IKernelCommandHandler<SubmitCode>
{
    public Task HandleAsync(SubmitCode command, KernelInvocationContext context)
    {
        object obj = "<div>Hello from Guitar Alchemist</div>";
        context.Display(obj);
        return Task.CompletedTask;
    }
}
