program Test;

uses
  Forms,
  uMain in 'uMain.pas' {Form1},
  uRegister in '..\..\Alchemist\uRegister.pas' {frmRegister};

{$R *.res}

begin
  Application.Initialize;
  Application.CreateForm(TForm1, Form1);
  Application.CreateForm(TfrmRegister, frmRegister);
  Application.Run;
end.
