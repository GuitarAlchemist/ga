using GA.Business.Core.Atonal;
using GA.Business.Core.Config;
using System.Collections.Generic;
using GA.Business.Core.Atonal.Primitives;
using GA.Business.Core.Scales;

[TestFixture]
public class ScaleConfigTests
{
    [Test]
    public void Test1()
    {
        var ids = PitchClassSetId.Items.Where(id => id.IsScale && !id.ToPitchClassSet().IsModal).Select(id => id.Value).ToImmutableList();
        var scales = ScalesConfigCache.Instance.GetAllScales().ToImmutableList();
        var scalesIds = scales.Select(value => value.PitchClassSet.Id.Value).ToImmutableHashSet();
        var missingIds = ids.Except(scalesIds);
        var sMissingIds = string.Join(", ", missingIds);

        var modalSetClasses = SetClass.ModalItems;
        var sModalSetClasses = string.Join(", ", modalSetClasses.Select(@class => @class.IntervalClassVector.Id.Value));
        
        var scaleByPcs = new Dictionary<PitchClassSet, List<ScalesConfigCache.ScaleCacheValue>>();
        foreach (var scale in scales)
        {
            if (!scaleByPcs.ContainsKey(scale.PitchClassSet)) scaleByPcs[scale.PitchClassSet] = [];
            scaleByPcs[scale.PitchClassSet].Add(scale);
        }

        Console.WriteLine("=====");
        Console.WriteLine($"{SetClass.Items.Count} set classes");
        Console.WriteLine("=====");
       
        foreach (var setClass in SetClass.Items)
        {
            var isModal = setClass.PrimeForm.IsModal;
            Console.WriteLine("");
            Console.WriteLine($"{setClass.PrimeForm} prime form - {setClass.IntervalClassVector} - IsModal: {isModal}");
            Console.WriteLine("");
            var primeForm = setClass.PrimeForm;
            
            if (scaleByPcs.TryGetValue(primeForm, out var matchingScales))
            {
                foreach (var scale in matchingScales)
                {
                    Console.WriteLine($"{scale.Scale.Name} : {scale.PitchClassSet}");
                }
            }
            
            if (primeForm.ModalFamily is { } modalFamily)
            {
                var modes = modalFamily.Modes;
                foreach (var mode in modes)
                {
                    Console.WriteLine($"[Mode] {mode}");
                }
            }
        }

        Assert.That(scales, Is.Not.Empty);
        Assert.That(scaleByPcs, Is.Not.Empty);
    }
}