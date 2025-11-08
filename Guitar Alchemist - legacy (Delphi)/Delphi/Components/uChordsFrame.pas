unit uChordsFrame;

interface

uses
  Windows, Messages, SysUtils, Variants, Classes, Graphics, Controls, Forms, 
  Dialogs, uTeChordTemplatePicker, te_controls, ExtCtrls,
  uTeVoicingDictionnary, ComCtrls, uTeScaleTree;

type
  TChordsFrame = class(TFrame)
    TeHeaderPanel1: TTeHeaderPanel;
    TeGroupBox1: TTeGroupBox;
    rbC: TTeRadioButton;
    TeRadioButton1: TTeRadioButton;
    TeRadioButton2: TTeRadioButton;
    TeRadioButton3: TTeRadioButton;
    TeRadioButton4: TTeRadioButton;
    TeRadioButton5: TTeRadioButton;
    TeRadioButton6: TTeRadioButton;
    TeRadioButton7: TTeRadioButton;
    TeRadioButton8: TTeRadioButton;
    TeRadioButton9: TTeRadioButton;
    TeRadioButton10: TTeRadioButton;
    TeRadioButton11: TTeRadioButton;
    TeMultiChordTemplatePicker1: TTeMultiChordTemplatePicker;
    TeHeaderPanel2: TTeHeaderPanel;
    TeScaleTree1: TTeScaleTree;
  private
    { Private declarations }
  public
    { Public declarations }
  end;

implementation

{$R *.dfm}

end.
