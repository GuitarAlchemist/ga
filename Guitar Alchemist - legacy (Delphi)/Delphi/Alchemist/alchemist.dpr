program alchemist;





uses
  Forms,
  uMain in 'uMain.pas' {frmMain},
  uHarmonizationFrame in 'uHarmonizationFrame.pas' {HarmonizationFrame: TFrame},
  uOptions in 'uOptions.pas' {frmOptions},
  uScaleFrame in 'uScaleFrame.pas' {ScaleFrame: TFrame},
  uChordsFrame in 'uChordsFrame.pas' {ChordsFrame: TFrame},
  uGlobalConstants in '..\Common\uGlobalConstants.pas',
  uVersionInformation in '..\Common\uVersionInformation.pas' {$IFDEF CheckRegistration},
  uRegister in 'uRegister.pas' {$ENDIF},
  uRegLicensing in 'uRegLicensing.pas',
  uVampsFrame in 'uVampsFrame.pas' {VampsFrame: TFrame},
  uPrintScale in 'uPrintScale.pas' {Form2},
  uStaffIntf in '..\..\GAII\uStaffIntf.pas';

{$R *.res}

begin
  Application.Initialize;
  Application.Title := 'Guitar Alchemist';
  Application.HelpFile := '';
  Application.CreateForm(TfrmMain, frmMain);
  Application.CreateForm(TfrmOptions, frmOptions);
  Application.CreateForm(TForm2, Form2);
  {$IFDEF CheckRegistration}
  Application.CreateForm(TfrmRegister, frmRegister);
{$ENDIF}  
  Application.Run;
end.
