object Form1: TForm1
  Left = 270
  Top = 106
  Width = 783
  Height = 526
  Caption = 'Form1'
  Color = clBtnFace
  Font.Charset = DEFAULT_CHARSET
  Font.Color = clWindowText
  Font.Height = -11
  Font.Name = 'MS Sans Serif'
  Font.Style = []
  OldCreateOrder = False
  OnCreate = FormCreate
  PixelsPerInch = 96
  TextHeight = 13
  object cbScaleName: TScaleBox
    Left = 312
    Top = 0
    Width = 361
    Height = 22
    ItemIndex = 4
    MinOffset = 0
    SelectedScaleName = 'major'
    ShowHarmonizableOnly = False
    TabOrder = 0
    OnChange = cbScaleNameChange
  end
  object cbScaleMode: TModeBox
    Left = 312
    Top = 32
    Width = 361
    Height = 22
    ColorDegrees = False
    DefaultDegreesColor = clNavy
    ItemIndex = 0
    MinOffset = 0
    ScaleName = 'Major'
    ShowCharacterTones = True
    ShowModeNumber = False
    TabOrder = 1
    OnChange = cbScaleModeChange
  end
  object ListBox1: TListBox
    Left = 8
    Top = 8
    Width = 289
    Height = 489
    ItemHeight = 13
    TabOrder = 2
    OnKeyDown = ListBox1KeyDown
  end
end
