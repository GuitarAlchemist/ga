namespace GaApi.Controllers.Api;

using GA.Data.EntityFramework.Data.Instruments;

[ApiController]
[Route("[controller]")]
public class InstrumentsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetInstruments()
    {
        var instruments = InstrumentsRepository.Instance.Instruments.Select(instrument => new
        {
            name = instrument.Name,
            icon = instrument.Icon,
            tunings = instrument.Tunings.Select(tuning => new
            {
                name = tuning.Value.Name,
                tuning = tuning.Value.Tuning
            })
        });

        return Ok(instruments);
    }
}
