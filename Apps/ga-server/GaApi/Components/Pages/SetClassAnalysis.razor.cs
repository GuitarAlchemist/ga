namespace GaApi.Components.Pages;

using GA.Business.Core.Atonal;
using GA.Business.Core.Atonal.Grothendieck;
using GA.Business.Core.Atonal.Primitives;
using GA.Business.Core.Scales;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Text;

public partial class SetClassAnalysis
{
    [Inject] private IGrothendieckService GrothendieckService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    // Spectral Analysis
    private string _spectralInput = "0,4,7"; // C major triad
    private bool _analyzingSpectrum;
    private SpectralAnalysisResult? _spectralResult;
    private string _magnitudeSpectrumChartHtml = string.Empty;
    private string _intervalVectorChartHtml = string.Empty;
    private string _nearestSetsChartHtml = string.Empty;

    // Grothendieck Analysis
    private string _grothendieckSourceInput = "0,2,4,5,7,9,11"; // C major scale
    private string _grothendieckTargetInput = "0,2,3,5,7,8,10"; // C natural minor scale
    private bool _analyzingGrothendieck;
    private GrothendieckAnalysisResult? _grothendieckResult;

    // Find Nearby
    private string _nearbyInput = "0,4,7"; // C major triad
    private int _maxDistance = 2;
    private bool _findingNearby;
    private List<NearbySetResult>? _nearbyResults;

    // DFT Analysis
    private string _dftInput = "0,4,7"; // C major triad
    private bool _analyzingDft;
    private DftAnalysisResult? _dftResult;
    private string _dftCoefficientsChartHtml = string.Empty;
    private string _phaseSpectrumChartHtml = string.Empty;

    // Shortest Path
    private string _pathSourceInput = "0,2,4,5,7,9,11"; // C major
    private string _pathTargetInput = "0,2,3,5,7,9,10"; // C Dorian
    private int _maxSteps = 5;
    private bool _findingPath;
    private List<PathStepResult>? _pathResults;

    private async Task AnalyzeSpectrum()
    {
        _analyzingSpectrum = true;
        try
        {
            var pitchClasses = ParsePitchClasses(_spectralInput);
            if (pitchClasses == null || pitchClasses.Length == 0)
            {
                Snackbar.Add("Invalid pitch class input. Please enter numbers 0-11 separated by commas or spaces.", Severity.Error);
                return;
            }

            var pitchClassSet = CreatePitchClassSet(pitchClasses);
            var setClass = new SetClass(pitchClassSet);

            // Get spectral metrics
            var magnitudeSpectrum = setClass.GetMagnitudeSpectrum();
            var spectralCentroid = setClass.GetSpectralCentroid();

            // Find nearest set classes by spectral similarity
            var nearestSetClasses = SetClassSpectralIndex.GetNearestBySpectrum(setClass, 8)
                .Select(sc => new NearestSetClassResult
                {
                    PrimeForm = sc.PrimeForm.ToString(),
                    IntervalClassVector = sc.IntervalClassVector.ToString(),
                    Distance = sc.GetSpectralDistance(setClass),
                    ModalFamily = sc.ModalFamily?.ToString()
                })
                .ToList();

            // Get scale name if available
            var scaleName = ScaleNameById.Get(setClass.PrimeForm.Id);

            // Calculate Forte number (simplified - based on cardinality and ICV id)
            var forteNumber = $"{setClass.Cardinality.Value}-{setClass.IntervalClassVector.Id.Value % 100}";

            _spectralResult = new SpectralAnalysisResult
            {
                PrimeForm = setClass.PrimeForm.ToString(),
                Cardinality = setClass.Cardinality.Value,
                IntervalClassVector = setClass.IntervalClassVector.ToString(),
                SpectralCentroid = spectralCentroid,
                MagnitudeSpectrum = magnitudeSpectrum,
                ModalFamily = setClass.ModalFamily?.ToString(),
                NearestSetClasses = nearestSetClasses,
                ForteNumber = forteNumber,
                ScaleName = string.IsNullOrEmpty(scaleName) ? null : scaleName,
                IanRingUrl = setClass.PrimeForm.ScalePageUrl.ToString(),
                PitchClassSetId = setClass.PrimeForm.Id
            };

            // Generate visualizations
            GenerateMagnitudeSpectrumChart(magnitudeSpectrum);
            GenerateIntervalVectorChart(setClass.IntervalClassVector);
            GenerateNearestSetsChart(nearestSetClasses);

            Snackbar.Add("Spectral analysis complete!", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error analyzing spectrum: {ex.Message}", Severity.Error);
        }
        finally
        {
            _analyzingSpectrum = false;
        }
    }

    private async Task AnalyzeDft()
    {
        _analyzingDft = true;
        try
        {
            var pitchClasses = ParsePitchClasses(_dftInput);
            if (pitchClasses == null || pitchClasses.Length == 0)
            {
                Snackbar.Add("Invalid pitch class input. Please enter numbers 0-11 separated by commas or spaces.", Severity.Error);
                return;
            }

            var pitchClassSet = CreatePitchClassSet(pitchClasses);
            var setClass = new SetClass(pitchClassSet);

            // Get DFT data
            var fourierCoefficients = setClass.GetFourierCoefficients();
            var phaseSpectrum = setClass.GetPhaseSpectrum();
            var magnitudeSpectrum = setClass.GetMagnitudeSpectrum();

            // Get scale name if available
            var scaleName = ScaleNameById.Get(setClass.PrimeForm.Id);

            // Calculate Forte number
            var forteNumber = $"{setClass.Cardinality.Value}-{setClass.IntervalClassVector.Id.Value % 100}";

            _dftResult = new DftAnalysisResult
            {
                PrimeForm = setClass.PrimeForm.ToString(),
                Cardinality = setClass.Cardinality.Value,
                IntervalClassVector = setClass.IntervalClassVector.ToString(),
                ForteNumber = forteNumber,
                FourierCoefficients = fourierCoefficients,
                PhaseSpectrum = phaseSpectrum,
                MagnitudeSpectrum = magnitudeSpectrum
            };

            // Generate visualizations
            GenerateDftCoefficientsChart(fourierCoefficients);
            GeneratePhaseSpectrumChart(phaseSpectrum);

            Snackbar.Add("DFT analysis complete!", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error analyzing DFT: {ex.Message}", Severity.Error);
        }
        finally
        {
            _analyzingDft = false;
        }
    }

    private async Task AnalyzeGrothendieck()
    {
        _analyzingGrothendieck = true;
        try
        {
            var sourcePitchClasses = ParsePitchClasses(_grothendieckSourceInput);
            var targetPitchClasses = ParsePitchClasses(_grothendieckTargetInput);

            if (sourcePitchClasses == null || targetPitchClasses == null)
            {
                Snackbar.Add("Invalid pitch class input. Please enter numbers 0-11 separated by commas or spaces.", Severity.Error);
                return;
            }

            var sourceIcv = GrothendieckService.ComputeIcv(sourcePitchClasses);
            var targetIcv = GrothendieckService.ComputeIcv(targetPitchClasses);
            var delta = GrothendieckService.ComputeDelta(sourceIcv, targetIcv);
            var harmonicCost = GrothendieckService.ComputeHarmonicCost(delta);

            _grothendieckResult = new GrothendieckAnalysisResult
            {
                SourceIcv = sourceIcv.ToString(),
                TargetIcv = targetIcv.ToString(),
                Delta = delta.ToString(),
                L1Norm = delta.L1Norm,
                HarmonicCost = harmonicCost,
                Explanation = delta.Explain()
            };

            Snackbar.Add("Grothendieck analysis complete!", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error analyzing transformation: {ex.Message}", Severity.Error);
        }
        finally
        {
            _analyzingGrothendieck = false;
        }
    }

    private async Task FindNearby()
    {
        _findingNearby = true;
        try
        {
            var pitchClasses = ParsePitchClasses(_nearbyInput);
            if (pitchClasses == null || pitchClasses.Length == 0)
            {
                Snackbar.Add("Invalid pitch class input. Please enter numbers 0-11 separated by commas or spaces.", Severity.Error);
                return;
            }

            var pitchClassSet = CreatePitchClassSet(pitchClasses);
            var nearby = GrothendieckService.FindNearby(pitchClassSet, _maxDistance);

            _nearbyResults = nearby
                .Select(result => new NearbySetResult
                {
                    PrimeForm = result.Set.PrimeForm?.ToString() ?? result.Set.ToString(),
                    IntervalClassVector = result.Set.IntervalClassVector.ToString(),
                    Delta = result.Delta.ToString(),
                    L1Norm = result.Delta.L1Norm,
                    Cost = result.Cost
                })
                .ToList();

            Snackbar.Add($"Found {_nearbyResults.Count} nearby set classes!", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error finding nearby sets: {ex.Message}", Severity.Error);
        }
        finally
        {
            _findingNearby = false;
        }
    }

    private async Task FindShortestPath()
    {
        _findingPath = true;
        try
        {
            var sourcePitchClasses = ParsePitchClasses(_pathSourceInput);
            var targetPitchClasses = ParsePitchClasses(_pathTargetInput);

            if (sourcePitchClasses == null || targetPitchClasses == null)
            {
                Snackbar.Add("Invalid pitch class input. Please enter numbers 0-11 separated by commas or spaces.", Severity.Error);
                return;
            }

            var sourcePitchClassSet = CreatePitchClassSet(sourcePitchClasses);
            var targetPitchClassSet = CreatePitchClassSet(targetPitchClasses);

            var path = GrothendieckService.FindShortestPath(sourcePitchClassSet, targetPitchClassSet, _maxSteps);

            var pathList = path.ToList();
            _pathResults = new List<PathStepResult>();

            for (int i = 0; i < pathList.Count; i++)
            {
                var current = pathList[i];
                var distanceToNext = 0;

                if (i < pathList.Count - 1)
                {
                    var next = pathList[i + 1];
                    var delta = GrothendieckService.ComputeDelta(
                        current.IntervalClassVector,
                        next.IntervalClassVector);
                    distanceToNext = delta.L1Norm;
                }

                _pathResults.Add(new PathStepResult
                {
                    PrimeForm = current.PrimeForm?.ToString() ?? current.ToString(),
                    IntervalClassVector = current.IntervalClassVector.ToString(),
                    DistanceToNext = distanceToNext
                });
            }

            if (_pathResults.Any())
            {
                Snackbar.Add($"Found path with {_pathResults.Count} steps!", Severity.Success);
            }
            else
            {
                Snackbar.Add($"No path found within {_maxSteps} steps.", Severity.Warning);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error finding path: {ex.Message}", Severity.Error);
        }
        finally
        {
            _findingPath = false;
        }
    }

    private static int[]? ParsePitchClasses(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        try
        {
            return input
                .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.Parse(s.Trim()))
                .Where(pc => pc >= 0 && pc <= 11)
                .ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static PitchClassSet CreatePitchClassSet(int[] pitchClasses)
    {
        var pcs = pitchClasses.Select(pc => PitchClass.FromValue(pc % 12)).ToList();
        return new PitchClassSet(pcs);
    }

    private static string GetDistanceCategory(int l1Norm)
    {
        return l1Norm switch
        {
            0 => "Identical",
            1 or 2 => "Very Close (Modal Interchange)",
            >= 3 and <= 5 => "Moderate Distance",
            _ => "Distant"
        };
    }

    private static Severity GetSeverityForDistance(int l1Norm)
    {
        return l1Norm switch
        {
            0 => Severity.Success,
            1 or 2 => Severity.Info,
            >= 3 and <= 5 => Severity.Normal,
            _ => Severity.Warning
        };
    }

    // Result classes
    private class SpectralAnalysisResult
    {
        public string PrimeForm { get; set; } = string.Empty;
        public int Cardinality { get; set; }
        public string IntervalClassVector { get; set; } = string.Empty;
        public double SpectralCentroid { get; set; }
        public double[] MagnitudeSpectrum { get; set; } = [];
        public string? ModalFamily { get; set; }
        public List<NearestSetClassResult> NearestSetClasses { get; set; } = [];
        public string? ForteNumber { get; set; }
        public string? ScaleName { get; set; }
        public string? IanRingUrl { get; set; }
        public PitchClassSetId? PitchClassSetId { get; set; }
    }

    private class NearestSetClassResult
    {
        public string PrimeForm { get; set; } = string.Empty;
        public string IntervalClassVector { get; set; } = string.Empty;
        public double Distance { get; set; }
        public string? ModalFamily { get; set; }
    }

    private class GrothendieckAnalysisResult
    {
        public string SourceIcv { get; set; } = string.Empty;
        public string TargetIcv { get; set; } = string.Empty;
        public string Delta { get; set; } = string.Empty;
        public int L1Norm { get; set; }
        public double HarmonicCost { get; set; }
        public string Explanation { get; set; } = string.Empty;
    }

    private class NearbySetResult
    {
        public string PrimeForm { get; set; } = string.Empty;
        public string IntervalClassVector { get; set; } = string.Empty;
        public string Delta { get; set; } = string.Empty;
        public int L1Norm { get; set; }
        public double Cost { get; set; }
    }

    private class PathStepResult
    {
        public string PrimeForm { get; set; } = string.Empty;
        public string IntervalClassVector { get; set; } = string.Empty;
        public int DistanceToNext { get; set; }
    }

    private class DftAnalysisResult
    {
        public string PrimeForm { get; set; } = string.Empty;
        public int Cardinality { get; set; }
        public string IntervalClassVector { get; set; } = string.Empty;
        public string? ForteNumber { get; set; }
        public System.Numerics.Complex[] FourierCoefficients { get; set; } = [];
        public double[] PhaseSpectrum { get; set; } = [];
        public double[] MagnitudeSpectrum { get; set; } = [];
    }

    // Chart generation methods using SVG
    private void GenerateMagnitudeSpectrumChart(double[] magnitudeSpectrum)
    {
        try
        {
            var maxValue = magnitudeSpectrum.Max();
            var chartHeight = 300;
            var chartWidth = 800;
            var barWidth = chartWidth / magnitudeSpectrum.Length - 10;

            var svg = new StringBuilder();
            svg.Append($"<svg width='{chartWidth}' height='{chartHeight + 60}' style='border: 1px solid #ddd; background: white;'>");
            svg.Append("<text x='400' y='20' text-anchor='middle' font-size='16' font-weight='bold'>Magnitude Spectrum (DFT)</text>");

            for (int i = 0; i < magnitudeSpectrum.Length; i++)
            {
                var barHeight = (magnitudeSpectrum[i] / maxValue) * chartHeight;
                var x = i * (chartWidth / magnitudeSpectrum.Length) + 5;
                var y = chartHeight + 30 - barHeight;

                svg.Append($"<rect x='{x}' y='{y}' width='{barWidth}' height='{barHeight}' fill='#2196F3' />");
                svg.Append($"<text x='{x + barWidth/2}' y='{chartHeight + 45}' text-anchor='middle' font-size='10'>k={i}</text>");
            }

            svg.Append("</svg>");
            _magnitudeSpectrumChartHtml = svg.ToString();
        }
        catch (Exception ex)
        {
            _magnitudeSpectrumChartHtml = $"<div class='alert alert-warning'>Chart generation error: {ex.Message}</div>";
        }
    }

    private void GenerateIntervalVectorChart(IntervalClassVector icv)
    {
        try
        {
            var intervalClasses = new[] { IntervalClass.Hemitone, IntervalClass.Tone, IntervalClass.FromValue(3),
                                         IntervalClass.FromValue(4), IntervalClass.FromValue(5), IntervalClass.Tritone };
            var intervalNames = new[] { "m2/M7", "M2/m7", "m3/M6", "M3/m6", "P4/P5", "TT" };
            var values = intervalClasses.Select(ic => icv[ic]).ToArray();

            var maxValue = values.Max();
            var chartSize = 400;
            var centerX = chartSize / 2;
            var centerY = chartSize / 2;
            var radius = 150;

            var svg = new StringBuilder();
            svg.Append($"<svg width='{chartSize}' height='{chartSize}' style='border: 1px solid #ddd; background: white;'>");
            svg.Append($"<text x='{centerX}' y='20' text-anchor='middle' font-size='14' font-weight='bold'>Interval Class Vector (Radar)</text>");

            // Draw radar chart
            var points = new List<string>();
            for (int i = 0; i < values.Length; i++)
            {
                var angle = (Math.PI * 2 * i / values.Length) - Math.PI / 2;
                var r = (values[i] / (double)maxValue) * radius;
                var x = centerX + r * Math.Cos(angle);
                var y = centerY + r * Math.Sin(angle);
                points.Add($"{x},{y}");

                // Draw axis lines
                var axisX = centerX + radius * Math.Cos(angle);
                var axisY = centerY + radius * Math.Sin(angle);
                svg.Append($"<line x1='{centerX}' y1='{centerY}' x2='{axisX}' y2='{axisY}' stroke='#ddd' stroke-width='1'/>");

                // Draw labels
                var labelX = centerX + (radius + 30) * Math.Cos(angle);
                var labelY = centerY + (radius + 30) * Math.Sin(angle);
                svg.Append($"<text x='{labelX}' y='{labelY}' text-anchor='middle' font-size='11'>{intervalNames[i]} ({values[i]})</text>");
            }

            svg.Append($"<polygon points='{string.Join(" ", points)}' fill='rgba(33, 150, 243, 0.3)' stroke='#2196F3' stroke-width='2'/>");
            svg.Append("</svg>");
            _intervalVectorChartHtml = svg.ToString();
        }
        catch (Exception ex)
        {
            _intervalVectorChartHtml = $"<div class='alert alert-warning'>Chart generation error: {ex.Message}</div>";
        }
    }

    private void GenerateNearestSetsChart(List<NearestSetClassResult> nearestSets)
    {
        try
        {
            var maxDistance = nearestSets.Max(s => s.Distance);
            var chartHeight = 300;
            var chartWidth = 800;
            var barWidth = chartWidth / nearestSets.Count - 10;

            var svg = new StringBuilder();
            svg.Append($"<svg width='{chartWidth}' height='{chartHeight + 80}' style='border: 1px solid #ddd; background: white;'>");
            svg.Append("<text x='400' y='20' text-anchor='middle' font-size='16' font-weight='bold'>Spectral Distance to Nearest Set Classes</text>");

            for (int i = 0; i < nearestSets.Count; i++)
            {
                var barHeight = (nearestSets[i].Distance / maxDistance) * chartHeight;
                var x = i * (chartWidth / nearestSets.Count) + 5;
                var y = chartHeight + 30 - barHeight;

                svg.Append($"<rect x='{x}' y='{y}' width='{barWidth}' height='{barHeight}' fill='#4CAF50' />");
                svg.Append($"<text x='{x + barWidth/2}' y='{chartHeight + 45}' text-anchor='middle' font-size='9' transform='rotate(45 {x + barWidth/2} {chartHeight + 45})'>{nearestSets[i].PrimeForm}</text>");
                svg.Append($"<text x='{x + barWidth/2}' y='{y - 5}' text-anchor='middle' font-size='9'>{nearestSets[i].Distance:F2}</text>");
            }

            svg.Append("</svg>");
            _nearestSetsChartHtml = svg.ToString();
        }
        catch (Exception ex)
        {
            _nearestSetsChartHtml = $"<div class='alert alert-warning'>Chart generation error: {ex.Message}</div>";
        }
    }

    private void GenerateDftCoefficientsChart(System.Numerics.Complex[] coefficients)
    {
        try
        {
            var maxReal = coefficients.Max(c => Math.Abs(c.Real));
            var maxImag = coefficients.Max(c => Math.Abs(c.Imaginary));
            var maxValue = Math.Max(maxReal, maxImag);

            var chartHeight = 300;
            var chartWidth = 800;
            var barWidth = (chartWidth / coefficients.Length - 10) / 2;

            var svg = new StringBuilder();
            svg.Append($"<svg width='{chartWidth}' height='{chartHeight + 60}' style='border: 1px solid #ddd; background: white;'>");
            svg.Append("<text x='400' y='20' text-anchor='middle' font-size='16' font-weight='bold'>DFT Coefficients (Real & Imaginary)</text>");

            // Draw zero line
            var zeroY = chartHeight / 2 + 30;
            svg.Append($"<line x1='0' y1='{zeroY}' x2='{chartWidth}' y2='{zeroY}' stroke='#999' stroke-width='1' stroke-dasharray='5,5'/>");

            for (int i = 0; i < coefficients.Length; i++)
            {
                var realHeight = (coefficients[i].Real / maxValue) * (chartHeight / 2);
                var imagHeight = (coefficients[i].Imaginary / maxValue) * (chartHeight / 2);

                var x = i * (chartWidth / coefficients.Length) + 5;

                // Real part (blue)
                var realY = realHeight >= 0 ? zeroY - realHeight : zeroY;
                var realBarHeight = Math.Abs(realHeight);
                svg.Append($"<rect x='{x}' y='{realY}' width='{barWidth}' height='{realBarHeight}' fill='#2196F3' />");

                // Imaginary part (orange)
                var imagY = imagHeight >= 0 ? zeroY - imagHeight : zeroY;
                var imagBarHeight = Math.Abs(imagHeight);
                svg.Append($"<rect x='{x + barWidth + 2}' y='{imagY}' width='{barWidth}' height='{imagBarHeight}' fill='#FF9800' />");

                // Label
                svg.Append($"<text x='{x + barWidth}' y='{chartHeight + 45}' text-anchor='middle' font-size='10'>k={i}</text>");
            }

            // Legend
            svg.Append($"<rect x='20' y='35' width='15' height='15' fill='#2196F3' />");
            svg.Append($"<text x='40' y='47' font-size='12'>Real</text>");
            svg.Append($"<rect x='100' y='35' width='15' height='15' fill='#FF9800' />");
            svg.Append($"<text x='120' y='47' font-size='12'>Imaginary</text>");

            svg.Append("</svg>");
            _dftCoefficientsChartHtml = svg.ToString();
        }
        catch (Exception ex)
        {
            _dftCoefficientsChartHtml = $"<div class='alert alert-warning'>Chart generation error: {ex.Message}</div>";
        }
    }

    private void GeneratePhaseSpectrumChart(double[] phaseSpectrum)
    {
        try
        {
            var chartHeight = 300;
            var chartWidth = 800;
            var barWidth = chartWidth / phaseSpectrum.Length - 10;

            var svg = new StringBuilder();
            svg.Append($"<svg width='{chartWidth}' height='{chartHeight + 60}' style='border: 1px solid #ddd; background: white;'>");
            svg.Append("<text x='400' y='20' text-anchor='middle' font-size='16' font-weight='bold'>Phase Spectrum</text>");

            // Draw zero line
            var zeroY = chartHeight / 2 + 30;
            svg.Append($"<line x1='0' y1='{zeroY}' x2='{chartWidth}' y2='{zeroY}' stroke='#999' stroke-width='1' stroke-dasharray='5,5'/>");

            // Draw +π and -π lines
            var piY = 30;
            var negPiY = chartHeight + 30;
            svg.Append($"<line x1='0' y1='{piY}' x2='{chartWidth}' y2='{piY}' stroke='#ddd' stroke-width='1'/>");
            svg.Append($"<line x1='0' y1='{negPiY}' x2='{chartWidth}' y2='{negPiY}' stroke='#ddd' stroke-width='1'/>");
            svg.Append($"<text x='5' y='25' font-size='10'>π</text>");
            svg.Append($"<text x='5' y='{negPiY - 5}' font-size='10'>-π</text>");

            for (int i = 0; i < phaseSpectrum.Length; i++)
            {
                // Normalize phase to [-π, π] range for display
                var phase = phaseSpectrum[i];
                var normalizedHeight = (phase / Math.PI) * (chartHeight / 2);

                var x = i * (chartWidth / phaseSpectrum.Length) + 5;
                var y = normalizedHeight >= 0 ? zeroY - normalizedHeight : zeroY;
                var barHeight = Math.Abs(normalizedHeight);

                svg.Append($"<rect x='{x}' y='{y}' width='{barWidth}' height='{barHeight}' fill='#9C27B0' />");
                svg.Append($"<text x='{x + barWidth/2}' y='{chartHeight + 45}' text-anchor='middle' font-size='10'>k={i}</text>");
            }

            svg.Append("</svg>");
            _phaseSpectrumChartHtml = svg.ToString();
        }
        catch (Exception ex)
        {
            _phaseSpectrumChartHtml = $"<div class='alert alert-warning'>Chart generation error: {ex.Message}</div>";
        }
    }
}

