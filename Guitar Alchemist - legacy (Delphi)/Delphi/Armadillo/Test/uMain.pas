unit uMain;

interface

uses
  Windows, Messages, SysUtils, Variants, Classes, Graphics, Controls, Forms,
  Dialogs, StdCtrls;

type
  TForm1 = class(TForm)
    Label1: TLabel;
    procedure FormShow(Sender: TObject);
  private
    { Private declarations }
  public
    { Public declarations }
  end;

var
  Form1: TForm1;

implementation

{$R *.dfm}

uses
   uArmadillo
   ,uRegister
   ;


procedure TForm1.FormShow(Sender: TObject);
var
   lMode : TLicensingModes;
begin
   lMode := frmRegister.TestTrial;
   label1.caption := IntToStr(Ord(lMode));
end;

end.
