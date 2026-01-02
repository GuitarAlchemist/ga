namespace GaApi.Components.Pages;

using GA.Business.Core.Atonal;
using GA.Business.Core.Atonal.Grothendieck;
using GA.Business.Core.Atonal.Primitives;
using GA.Business.Core.Unified;
using GA.Business.Core.Scales;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Text;
using Microsoft.JSInterop;
using GA.Business.Core.Fretboard.Shapes.Geometry;

public partial class SetClassAnalysis
{
    [Inject] private IGrothendieckService GrothendieckService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;
    [Inject] private IUnifiedModeService UnifiedModeService { get; set; } = null!;

    // Spectral Analysis
    private string _spectralInput = "0,4,7"; // C major triad
    private bool _analyzingSpectrum;
    private SpectralAnalysisResult? _spectralResult;
    private string _magnitudeSpectrumChartHtml = string.Empty;
    private string _intervalVectorChartHtml = string.Empty;
    private string _nearestSetsChartHtml = string.Empty;

    // OPTIC options (user-toggleable)
    private bool _opticOctave = true;        // O
    private bool _opticPermutation = true;   // P
    private bool _opticTransposition = true; // T
    private bool _opticInversion = false;    // I (off by default)

    // Persistence keys
    private const string OpticOctaveKey = "ga.optic.o";
    private const string OpticPermKey = "ga.optic.p";
    private const string OpticTransKey = "ga.optic.t";
    private const string OpticInvKey = "ga.optic.i";

    // Voice-leading tools state
    private string _vlProbeA = "0,4,7";
    private string _vlProbeB = "11,3,6";
    private double? _vlProbeDistance;
    private bool _probingDistance;

    private string _vlSourceInput = "0,4,7";
    private string _vlTargetInput = "11,3,6";
    private int _vlSteps = 12;
    private string _geodesicSvgHtml = string.Empty;
    private bool _analyzingGeodesic;

    private string _heatmapTargetInput = "0,7"; // two-voice target: perfect fifth
    private int _heatmapResolution = 48;
    private string _heatmapSvgHtml = string.Empty;
    private bool _generatingHeatmap;

    private string _opticGraphSvgHtml = string.Empty;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        try
        {
            // Load persisted toggle values if present
            var o = await JS.InvokeAsync<string?>("localStorage.getItem", OpticOctaveKey);
            var p = await JS.InvokeAsync<string?>("localStorage.getItem", OpticPermKey);
            var t = await JS.InvokeAsync<string?>("localStorage.getItem", OpticTransKey);
            var i = await JS.InvokeAsync<string?>("localStorage.getItem", OpticInvKey);

            if (bool.TryParse(o ?? string.Empty, out var ob)) _opticOctave = ob;
            if (bool.TryParse(p ?? string.Empty, out var pb)) _opticPermutation = pb;
            if (bool.TryParse(t ?? string.Empty, out var tb)) _opticTransposition = tb;
            if (bool.TryParse(i ?? string.Empty, out var ib)) _opticInversion = ib;

            StateHasChanged();
        }
        catch
        {
            // Ignore storage errors; fall back to defaults
        }
    }

    private async Task PersistOpticAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("localStorage.setItem", OpticOctaveKey, _opticOctave.ToString());
            await JS.InvokeVoidAsync("localStorage.setItem", OpticPermKey, _opticPermutation.ToString());
            await JS.InvokeVoidAsync("localStorage.setItem", OpticTransKey, _opticTransposition.ToString());
            await JS.InvokeVoidAsync("localStorage.setItem", OpticInvKey, _opticInversion.ToString());
        }
        catch
        {
            // Non-fatal
        }
    }

    private async Task OnOctaveChanged(bool value)
    {
        _opticOctave = value;
        await PersistOpticAsync();
    }

    private async Task OnPermutationChanged(bool value)
    {
        _opticPermutation = value;
        await PersistOpticAsync();
    }

    private async Task OnTranspositionChanged(bool value)
    {
        _opticTransposition = value;
        await PersistOpticAsync();
    }

    private async Task OnInversionChanged(bool value)
    {
        _opticInversion = value;
        await PersistOpticAsync();
    }

    private async Task ResetOpticDefaults()
    {
        _opticOctave = true;
        _opticPermutation = true;
        _opticTransposition = true;
        _opticInversion = false;
        await PersistOpticAsync();
        Snackbar.Add("OPTIC options reset to defaults (OPT)", Severity.Info);
    }

    // =====================
    // Voice-leading tools
    // =====================

    private Task ProbeOpticDistance()
    {
        if (_probingDistance) return Task.CompletedTask;
        _probingDistance = true;
        try
        {
            var a = ParsePitchClasses(_vlProbeA);
            var b = ParsePitchClasses(_vlProbeB);
            if (a == null || b == null || a.Length == 0 || b.Length == 0)
            {
                Snackbar.Add("Enter valid pitch-class sets for A and B.", Severity.Error);
                return Task.CompletedTask;
            }

            var scA = new SetClass(CreatePitchClassSet(a));
            var scB = new SetClass(CreatePitchClassSet(b));

            var options = new VoiceLeadingOptions
            {
                OctaveEquivalence = _opticOctave,
                PermutationEquivalence = _opticPermutation,
                TranspositionEquivalence = _opticTransposition,
                InversionEquivalence = _opticInversion
            };

            _vlProbeDistance = SetClassOpticIndex.Distance(scA, scB, options);
        }
        catch (Exception ex)
        {
            _vlProbeDistance = null;
            Snackbar.Add($"Distance probe failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            _probingDistance = false;
        }
        return Task.CompletedTask;
    }

    private Task AnalyzeGeodesic()
    {
        if (_analyzingGeodesic) return Task.CompletedTask;
        _analyzingGeodesic = true;
        try
        {
            var src = ParsePitchClasses(_vlSourceInput);
            var dst = ParsePitchClasses(_vlTargetInput);
            if (src == null || dst == null || src.Length == 0 || dst.Length == 0)
            {
                Snackbar.Add("Enter valid pitch-class sets for Source and Target.", Severity.Error);
                return Task.CompletedTask;
            }

            var vFrom = src.Select(x => (double)((x % 12 + 12) % 12)).ToArray();
            var vToRaw = dst.Select(x => (double)((x % 12 + 12) % 12)).ToArray();
            var n = Math.Max(vFrom.Length, vToRaw.Length);
            vFrom = ExpandToCardinality(vFrom, n);
            var vTo = ExpandToCardinality(vToRaw, n);

            var space = new VoiceLeadingSpace(
                voices: n,
                octaveEquivalence: _opticOctave,
                permutationEquivalence: _opticPermutation,
                transpositionEquivalence: _opticTransposition,
                inversionEquivalence: _opticInversion);

            var steps = System.Math.Clamp(_vlSteps, 2, 64);
            var path = space.Geodesic(vFrom, vTo, steps);
            _geodesicSvgHtml = RenderGeodesicSvg(path, width: 720, height: 220);
        }
        catch (Exception ex)
        {
            _geodesicSvgHtml = string.Empty;
            Snackbar.Add($"Geodesic computation failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            _analyzingGeodesic = false;
        }
        return Task.CompletedTask;
    }

    private Task GenerateTwoVoiceHeatmap()
    {
        if (_generatingHeatmap) return Task.CompletedTask;
        _generatingHeatmap = true;
        try
        {
            var tgt = ParsePitchClasses(_heatmapTargetInput);
            if (tgt == null || tgt.Length == 0)
            {
                Snackbar.Add("Enter a valid two-voice target (e.g., 0,7).", Severity.Error);
                return Task.CompletedTask;
            }

            // Use first two classes; if single provided, duplicate
            double[] target = tgt.Length >= 2
                ? new[] { (double)(tgt[0] % 12), (double)(tgt[1] % 12) }
                : new[] { (double)(tgt[0] % 12), (double)(tgt[0] % 12) };

            var res = System.Math.Clamp(_heatmapResolution, 16, 96);
            _heatmapSvgHtml = RenderTwoVoiceHeatmapSvg(target, res, width: 360, height: 360);
        }
        catch (Exception ex)
        {
            _heatmapSvgHtml = string.Empty;
            Snackbar.Add($"Heatmap generation failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            _generatingHeatmap = false;
        }
        return Task.CompletedTask;
    }

    private Task BuildOpticNeighborGraphFromCurrent()
    {
        try
        {
            if (_spectralResult == null || _spectralResult.NearestOpticSetClasses.Count == 0)
            {
                Snackbar.Add("Analyze a set first to get OPTIC neighbors.", Severity.Info);
                return Task.CompletedTask;
            }
            _opticGraphSvgHtml = RenderOpticNeighborGraphSvg(_spectralResult.NearestOpticSetClasses, 720, 420);
        }
        catch (Exception ex)
        {
            _opticGraphSvgHtml = string.Empty;
            Snackbar.Add($"Failed to build neighbor graph: {ex.Message}", Severity.Error);
        }
        return Task.CompletedTask;
    }

    // =====================
    // Rendering helpers (SVG)
    // =====================

    private static double[] ExpandToCardinality(double[] v, int target)
    {
        if (v.Length == target) return v;
        if (v.Length == 0) return new double[target];
        var result = new double[target];
        for (var i = 0; i < target; i++) result[i] = v[i % v.Length];
        return result;
    }

    private static string RenderGeodesicSvg(List<double[]> path, int width, int height)
    {
        if (path.Count == 0) return string.Empty;
        var n = path[0].Length;
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}' viewBox='0 0 {width} {height}'>");
        sb.Append("<rect width='100%' height='100%' fill='white' />");

        // axes
        sb.Append("<line x1='40' y1='10' x2='40' y2='" + (height - 30) + "' stroke='#ccc' stroke-width='1'/>");
        sb.Append("<line x1='40' y1='" + (height - 30) + "' x2='" + (width - 10) + "' y2='" + (height - 30) + "' stroke='#ccc' stroke-width='1'/>");
        sb.Append("<text x='5' y='20' font-size='10' fill='#666'>pc</text>");

        // color palette
        string[] colors = ["#e41a1c", "#377eb8", "#4daf4a", "#984ea3", "#ff7f00", "#a65628", "#f781bf", "#999999"];

        double left = 50, right = width - 20; int steps = path.Count;
        double usableW = right - left;
        double dx = usableW / System.Math.Max(1, steps - 1);
        double top = 10, bottom = height - 40;

        double Y(double pc) => bottom - (pc % 12.0) / 12.0 * (bottom - top);

        // draw polylines per voice
        for (int v = 0; v < n; v++)
        {
            var pts = new StringBuilder();
            for (int i = 0; i < steps; i++)
            {
                double x = left + dx * i;
                double y = Y(path[i][v]);
                pts.Append($"{x},{y} ");
            }
            var color = colors[v % colors.Length];
            sb.Append($"<polyline fill='none' stroke='{color}' stroke-width='2' points='{pts}' />");
        }

        // draw markers
        for (int i = 0; i < steps; i++)
        {
            double x = left + dx * i;
            sb.Append($"<circle cx='{x}' cy='{bottom}' r='2' fill='#888' />");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    private string RenderTwoVoiceHeatmapSvg(double[] target, int res, int width, int height)
    {
        var space = new VoiceLeadingSpace(2, _opticOctave, _opticPermutation, _opticTransposition, _opticInversion);
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}' viewBox='0 0 {width} {height}'>");
        sb.Append("<rect width='100%' height='100%' fill='white' />");

        double cellW = (double)width / res;
        double cellH = (double)height / res;

        for (int i = 0; i < res; i++)
        {
            for (int j = 0; j < res; j++)
            {
                double xpc = i * 12.0 / res; // [0,12)
                double ypc = j * 12.0 / res;
                var d = space.Distance(new[] { xpc, ypc }, target);
                // Normalize distance into [0,1] roughly; cap at 6
                double t = System.Math.Min(System.Math.Abs(d) / 6.0, 1.0);
                var color = LerpColor("#2ecc71", "#e74c3c", t); // green->red
                double x = i * cellW;
                double y = (res - 1 - j) * cellH; // invert y so low pc at bottom
                sb.Append($"<rect x='{x:F2}' y='{y:F2}' width='{cellW + 0.5:F2}' height='{cellH + 0.5:F2}' fill='{color}' stroke='none' />");
            }
        }

        sb.Append("</svg>");
        return sb.ToString();

        static string LerpColor(string hexA, string hexB, double t)
        {
            (int r1, int g1, int b1) = HexToRgb(hexA);
            (int r2, int g2, int b2) = HexToRgb(hexB);
            int r = (int)System.Math.Round(r1 + (r2 - r1) * t);
            int g = (int)System.Math.Round(g1 + (g2 - g1) * t);
            int b = (int)System.Math.Round(b1 + (b2 - b1) * t);
            return $"rgb({r},{g},{b})";
        }

        static (int, int, int) HexToRgb(string hex)
        {
            hex = hex.TrimStart('#');
            int r = Convert.ToInt32(hex.Substring(0, 2), 16);
            int g = Convert.ToInt32(hex.Substring(2, 2), 16);
            int b = Convert.ToInt32(hex.Substring(4, 2), 16);
            return (r, g, b);
        }
    }

    private static string RenderOpticNeighborGraphSvg(List<OpticNearestSetClassResult> neighbors, int width, int height)
    {
        if (neighbors.Count == 0) return string.Empty;
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}' viewBox='0 0 {width} {height}'>");
        sb.Append("<rect width='100%' height='100%' fill='white' />");

        double cx = width / 2.0, cy = height / 2.0; double radius = System.Math.Min(width, height) * 0.35;
        int n = neighbors.Count; double angleStep = 2 * Math.PI / n;

        // node positions
        var pts = new (double x, double y)[n];
        for (int i = 0; i < n; i++)
        {
            double ang = i * angleStep - Math.PI / 2;
            pts[i] = (cx + radius * Math.Cos(ang), cy + radius * Math.Sin(ang));
        }

        // edges to nearest few (e.g., 3)
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            double d = neighbors[i].OpticDistance;
            double sw = Math.Max(0.5, 3.5 - d); // smaller distance => thicker line
            sb.Append($"<line x1='{pts[i].x:F2}' y1='{pts[i].y:F2}' x2='{pts[j].x:F2}' y2='{pts[j].y:F2}' stroke='#888' stroke-width='{sw:F1}' opacity='0.6' />");
        }

        // nodes and labels
        for (int i = 0; i < n; i++)
        {
            sb.Append($"<circle cx='{pts[i].x:F2}' cy='{pts[i].y:F2}' r='10' fill='#1976d2' />");
            var label = System.Net.WebUtility.HtmlEncode(neighbors[i].PrimeForm);
            sb.Append($"<text x='{pts[i].x + 12:F2}' y='{pts[i].y + 4:F2}' font-size='12' fill='#333'>{label}</text>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    // Unified Mode summary (read-only panel)
    private UnifiedModeDescription? _unifiedDesc;

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

    // Display: Set-class label notation (Forte default, toggleable to Rahn)
    private SetClassNotation _selectedNotation = SetClassNotation.Forte;

    private string GetSetClassLabel(string primeForm)
    {
        try
        {
            var pcs = PitchClassSet.Parse(primeForm);
            var sc = new SetClass(pcs);
            return SetClassLabelFormatter.ToLabel(sc, _selectedNotation);
        }
        catch
        {
            return "-";
        }
    }

    private Task AnalyzeSpectrum()
    {
        _analyzingSpectrum = true;
        try
        {
            var pitchClasses = ParsePitchClasses(_spectralInput);
            if (pitchClasses == null || pitchClasses.Length == 0)
            {
                Snackbar.Add("Invalid pitch class input. Please enter numbers 0-11 separated by commas or spaces.", Severity.Error);
                return Task.CompletedTask;
            }

            var pitchClassSet = CreatePitchClassSet(pitchClasses);
            var setClass = new SetClass(pitchClassSet);

            // Get spectral metrics
            var magnitudeSpectrum = setClass.GetMagnitudeSpectrum();
            var spectralCentroid = setClass.GetSpectralCentroid();

            // Find nearest set classes by spectral similarity
            var nearestSetClasses = SetClassSpectralIndex.GetNearestBySpectrum(setClass)
                .Select(sc => new NearestSetClassResult
                {
                    PrimeForm = sc.PrimeForm.ToString(),
                    IntervalClassVector = sc.IntervalClassVector.ToString(),
                    Distance = sc.GetSpectralDistance(setClass),
                    ModalFamily = sc.ModalFamily?.ToString()
                })
                .ToList();

            // Find nearest set classes by OPTIC (voice-leading) geometry using user toggles
            var opticOptions = new VoiceLeadingOptions
            {
                OctaveEquivalence = _opticOctave,
                PermutationEquivalence = _opticPermutation,
                TranspositionEquivalence = _opticTransposition,
                InversionEquivalence = _opticInversion
            };
            var nearestOptic = SetClassOpticIndex.GetNearestByOptic(setClass, 10, opticOptions)
                .Select(t => new OpticNearestSetClassResult
                {
                    PrimeForm = t.setClass.PrimeForm.ToString(),
                    IntervalClassVector = t.setClass.IntervalClassVector.ToString(),
                    OpticDistance = t.distance,
                    ModalFamily = t.setClass.ModalFamily?.ToString()
                })
                .ToList();

            // Get scale name if available
            var scaleName = ScaleNameById.Get(setClass.PrimeForm.Id);

            // Forte number: use catalog mapping when available; otherwise leave null (display fallback in UI)
            string? forteNumber = null;
            if (ForteCatalog.TryGetForteNumber(setClass.PrimeForm, out var forte))
            {
                forteNumber = forte.ToString();
            }

            _spectralResult = new SpectralAnalysisResult
            {
                PrimeForm = setClass.PrimeForm.ToString(),
                Cardinality = setClass.Cardinality.Value,
                IntervalClassVector = setClass.IntervalClassVector.ToString(),
                SpectralCentroid = spectralCentroid,
                MagnitudeSpectrum = magnitudeSpectrum,
                ModalFamily = setClass.ModalFamily?.ToString(),
                NearestSetClasses = nearestSetClasses,
                NearestOpticSetClasses = nearestOptic,
                ForteNumber = forteNumber,
                ScaleName = string.IsNullOrEmpty(scaleName) ? null : scaleName,
                IanRingUrl = setClass.PrimeForm.ScalePageUrl.ToString(),
                PitchClassSetId = setClass.PrimeForm.Id
            };

            // Unified summary via unified mode service (root at C)
            try
            {
                var unified = UnifiedModeService.FromPitchClassSet(pitchClassSet, PitchClass.C);
                _unifiedDesc = UnifiedModeService.Describe(unified);
            }
            catch
            {
                _unifiedDesc = null;
            }

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

        return Task.CompletedTask;
    }

    private Task AnalyzeDft()
    {
        _analyzingDft = true;
        try
        {
            var pitchClasses = ParsePitchClasses(_dftInput);
            if (pitchClasses == null || pitchClasses.Length == 0)
            {
                Snackbar.Add("Invalid pitch class input. Please enter numbers 0-11 separated by commas or spaces.", Severity.Error);
                return Task.CompletedTask;
            }

            var pitchClassSet = CreatePitchClassSet(pitchClasses);
            var setClass = new SetClass(pitchClassSet);

            // Get DFT data
            var fourierCoefficients = setClass.GetFourierCoefficients();
            var phaseSpectrum = setClass.GetPhaseSpectrum();
            var magnitudeSpectrum = setClass.GetMagnitudeSpectrum();

            // Get scale name if available
            var scaleName = ScaleNameById.Get(setClass.PrimeForm.Id);

            // Forte number via catalog mapping if available
            string? forteNumber = null;
            if (ForteCatalog.TryGetForteNumber(setClass.PrimeForm, out var forte))
            {
                forteNumber = forte.ToString();
            }

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

        return Task.CompletedTask;
    }

    private Task AnalyzeGrothendieck()
    {
        _analyzingGrothendieck = true;
        try
        {
            var sourcePitchClasses = ParsePitchClasses(_grothendieckSourceInput);
            var targetPitchClasses = ParsePitchClasses(_grothendieckTargetInput);

            if (sourcePitchClasses == null || targetPitchClasses == null)
            {
                Snackbar.Add("Invalid pitch class input. Please enter numbers 0-11 separated by commas or spaces.", Severity.Error);
                return Task.CompletedTask;
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

        return Task.CompletedTask;
    }

    private Task FindNearby()
    {
        _findingNearby = true;
        try
        {
            var pitchClasses = ParsePitchClasses(_nearbyInput);
            if (pitchClasses == null || pitchClasses.Length == 0)
            {
                Snackbar.Add("Invalid pitch class input. Please enter numbers 0-11 separated by commas or spaces.", Severity.Error);
                return Task.CompletedTask;
            }

            var pitchClassSet = CreatePitchClassSet(pitchClasses);
            var nearby = GrothendieckService.FindNearby(pitchClassSet, _maxDistance);

            _nearbyResults = [.. nearby
                .Select(result => new NearbySetResult
                {
                    PrimeForm = result.Set.PrimeForm?.ToString() ?? result.Set.ToString(),
                    IntervalClassVector = result.Set.IntervalClassVector.ToString(),
                    Delta = result.Delta.ToString(),
                    L1Norm = result.Delta.L1Norm,
                    Cost = result.Cost
                })];

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

        return Task.CompletedTask;
    }

    private Task FindShortestPath()
    {
        _findingPath = true;
        try
        {
            var sourcePitchClasses = ParsePitchClasses(_pathSourceInput);
            var targetPitchClasses = ParsePitchClasses(_pathTargetInput);

            if (sourcePitchClasses == null || targetPitchClasses == null)
            {
                Snackbar.Add("Invalid pitch class input. Please enter numbers 0-11 separated by commas or spaces.", Severity.Error);
                return Task.CompletedTask;
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

        return Task.CompletedTask;
    }

    private static int[]? ParsePitchClasses(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        try
        {
            return [.. input
                .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.Parse(s.Trim()))
                .Where(pc => pc >= 0 && pc <= 11)];
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
        public List<OpticNearestSetClassResult> NearestOpticSetClasses { get; set; } = [];
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

    private class OpticNearestSetClassResult
    {
        public string PrimeForm { get; set; } = string.Empty;
        public string IntervalClassVector { get; set; } = string.Empty;
        public double OpticDistance { get; set; }
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

