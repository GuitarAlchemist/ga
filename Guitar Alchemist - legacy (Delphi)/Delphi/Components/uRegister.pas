unit uRegister;

interface

uses
   Classes
   ,uScalePatternFinder
   ,uStaff
   ,uTeChordTemplateBox
   ,uFingerBoard
   ,uTeKeyBox
   ,uTeVoicingDictionnary
   ,uTeScaleTree
   ,uTeNumFuncPicker
   ,uTeChordTemplatePicker
   ,uScalePattern
   ;

procedure Register;

implementation

procedure Register;
begin
  RegisterComponents('Guitar', [TScalePatternFinder]);
  RegisterComponents('Guitar', [TTeScaleTree]);
  RegisterComponents('Guitar', [TTeKeyBox]);
  RegisterComponents('Guitar', [TTeVoicingDictionnary]);
  RegisterComponents('Guitar', [TTeChordTemplateList]);
  RegisterComponents('Guitar', [TScalePatternChart]);

  RegisterComponents('Guitar', [TFingerboard]);

  RegisterComponents('Guitar', [TTeNumericFunctionPicker]);

  RegisterComponents('Guitar', [TTeChordTemplatePicker]);
  RegisterComponents('Guitar', [TTeMultiChordTemplatePicker]);

  RegisterComponents('Guitar', [TScalePattern]);  
end;

end.
