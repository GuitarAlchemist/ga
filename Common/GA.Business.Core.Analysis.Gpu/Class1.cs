// Assembly-wide attributes for GA.Business.Core.Analysis.Gpu.
//
// [assembly: ExcludeFromCodeCoverage]
// ------------------------------------
// ILGPU compiles kernel methods (e.g. SetClassGpuAnalyzer.ComputeMagnitudeKernel)
// at runtime by walking their CLR call graph. Coverlet code-coverage instrumentation
// injects calls to a mutable static `HitsArray` into every method body — which ILGPU
// rejects with:
//
//   ILGPU.InternalCompilerException
//     ---> System.NotSupportedException: Cannot load from the static field
//          'Int32[] HitsArray' since it is not read only
//
// Tests that load the kernel (SetClassGpuAnalyzerTests + SetClassGpuAnalyzerProvider
// tests) therefore fail whenever the test run is collected with `--collect:"XPlat
// Code Coverage"`, which is the default in the CI/CD Pipeline's Backend Tests job.
// PR #201 surfaced this by unblocking Backend Tests from a static-web-assets crash
// that previously hid the failure.
//
// Excluding the assembly is the right scope: GPU kernel coverage isn't meaningful
// (the code that runs is compiled to PTX/OpenCL, not the CLR being measured), and
// the project is small (one analyzer + one provider + one DTO), so we don't lose
// signal worth keeping. .runsettings carries a redundant `<Exclude>` clause for
// runners that honor it.

[assembly: System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
