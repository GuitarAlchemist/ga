object ChordsFrame: TChordsFrame
  Left = 0
  Top = 0
  Width = 717
  Height = 475
  Align = alClient
  TabOrder = 0
  object TeHeaderPanel1: TTeHeaderPanel
    Left = 0
    Top = 0
    Width = 357
    Height = 475
    Performance = kspNoBuffer
    Align = alLeft
    AnimateRoll = False
    BevelWidth = 1
    BorderWidth = 3
    ButtonKind = kpbkRollUp
    Caption = 'TeHeaderPanel1'
    CaptionHeight = 20
    LinkPanels = False
    Rolled = False
    ParentRoll = False
    ShowBevel = False
    ShowButton = False
    ShowCaption = False
    ThemeObject = 'default'
    NormalHeight = {00000000}
    object TeGroupBox1: TTeGroupBox
      Left = 3
      Top = 3
      Width = 351
      Height = 70
      Performance = kspNoBuffer
      Align = alTop
      CaptionMargin = 12
      Caption = 'Chord'
      CheckBoxAlignment = kalLeftJustify
      ThemeObject = 'default'
      Transparent = True
      TabOrder = 0
      UseCheckBox = False
      object rbC: TTeRadioButton
        Left = 7
        Top = 37
        Width = 48
        Height = 18
        Performance = kspDoubleBuffer
        Caption = 'C'
        Alignment = kalLeftJustify
        Checked = True
        GroupIndex = 0
        Spacing = 2
        ThemeObject = 'default'
        TabOrder = 0
        TabStop = True
        Transparent = True
        WordWrap = False
      end
      object TeRadioButton1: TTeRadioButton
        Tag = 2
        Left = 56
        Top = 37
        Width = 48
        Height = 18
        Performance = kspDoubleBuffer
        Caption = 'D'
        Alignment = kalLeftJustify
        Checked = False
        GroupIndex = 0
        Spacing = 2
        ThemeObject = 'default'
        TabOrder = 1
        Transparent = True
        WordWrap = False
      end
      object TeRadioButton2: TTeRadioButton
        Tag = 4
        Left = 105
        Top = 37
        Width = 48
        Height = 18
        Performance = kspDoubleBuffer
        Caption = 'E'
        Alignment = kalLeftJustify
        Checked = False
        GroupIndex = 0
        Spacing = 2
        ThemeObject = 'default'
        TabOrder = 2
        Transparent = True
        WordWrap = False
      end
      object TeRadioButton3: TTeRadioButton
        Tag = 5
        Left = 154
        Top = 37
        Width = 48
        Height = 18
        Performance = kspDoubleBuffer
        Caption = 'F'
        Alignment = kalLeftJustify
        Checked = False
        GroupIndex = 0
        Spacing = 2
        ThemeObject = 'default'
        TabOrder = 3
        Transparent = True
        WordWrap = False
      end
      object TeRadioButton4: TTeRadioButton
        Tag = 7
        Left = 203
        Top = 37
        Width = 48
        Height = 18
        Performance = kspDoubleBuffer
        Caption = 'G'
        Alignment = kalLeftJustify
        Checked = False
        GroupIndex = 0
        Spacing = 2
        ThemeObject = 'default'
        TabOrder = 4
        Transparent = True
        WordWrap = False
      end
      object TeRadioButton5: TTeRadioButton
        Tag = 9
        Left = 252
        Top = 37
        Width = 48
        Height = 18
        Performance = kspDoubleBuffer
        Caption = 'A'
        Alignment = kalLeftJustify
        Checked = False
        GroupIndex = 0
        Spacing = 2
        ThemeObject = 'default'
        TabOrder = 5
        Transparent = True
        WordWrap = False
      end
      object TeRadioButton6: TTeRadioButton
        Tag = 11
        Left = 301
        Top = 37
        Width = 25
        Height = 18
        Performance = kspDoubleBuffer
        Caption = 'B'
        Alignment = kalLeftJustify
        Checked = False
        GroupIndex = 0
        Spacing = 2
        ThemeObject = 'default'
        TabOrder = 6
        Transparent = True
        WordWrap = False
      end
      object TeRadioButton7: TTeRadioButton
        Tag = 1
        Left = 33
        Top = 17
        Width = 48
        Height = 18
        Performance = kspDoubleBuffer
        Caption = 'C#/Db'
        Alignment = kalLeftJustify
        Checked = False
        GroupIndex = 0
        Font.Charset = DEFAULT_CHARSET
        Font.Color = clBlack
        Font.Height = -11
        Font.Name = 'MS Sans Serif'
        Font.Style = []
        Spacing = 2
        ThemeObject = 'default'
        TabOrder = 7
        Transparent = True
        WordWrap = False
      end
      object TeRadioButton8: TTeRadioButton
        Tag = 3
        Left = 84
        Top = 17
        Width = 48
        Height = 18
        Performance = kspDoubleBuffer
        Caption = 'D#/Eb'
        Alignment = kalLeftJustify
        Checked = False
        GroupIndex = 0
        Spacing = 2
        ThemeObject = 'default'
        TabOrder = 8
        Transparent = True
        WordWrap = False
      end
      object TeRadioButton9: TTeRadioButton
        Tag = 6
        Left = 180
        Top = 17
        Width = 48
        Height = 18
        Performance = kspDoubleBuffer
        Caption = 'F#/Gb'
        Alignment = kalLeftJustify
        Checked = False
        GroupIndex = 0
        Spacing = 2
        ThemeObject = 'default'
        TabOrder = 9
        Transparent = True
        WordWrap = False
      end
      object TeRadioButton10: TTeRadioButton
        Tag = 8
        Left = 229
        Top = 17
        Width = 48
        Height = 18
        Performance = kspDoubleBuffer
        Caption = 'G#/Ab'
        Alignment = kalLeftJustify
        Checked = False
        GroupIndex = 0
        Spacing = 2
        ThemeObject = 'default'
        TabOrder = 10
        Transparent = True
        WordWrap = False
      end
      object TeRadioButton11: TTeRadioButton
        Tag = 10
        Left = 278
        Top = 17
        Width = 48
        Height = 18
        Performance = kspDoubleBuffer
        Caption = 'A#/Bb'
        Alignment = kalLeftJustify
        Checked = False
        GroupIndex = 0
        Spacing = 2
        ThemeObject = 'default'
        TabOrder = 11
        Transparent = True
        WordWrap = False
      end
    end
    object TeMultiChordTemplatePicker1: TTeMultiChordTemplatePicker
      Left = 3
      Top = 73
      Width = 351
      Height = 399
      Align = alClient
      ScaleRoot = htC
      Key = ksGFlatMajorEFlatMinor
      DegreesOffset = 143
    end
  end
  object TeHeaderPanel2: TTeHeaderPanel
    Left = 357
    Top = 0
    Width = 360
    Height = 475
    Performance = kspNoBuffer
    Align = alClient
    AnimateRoll = False
    BevelWidth = 0
    BorderWidth = 0
    ButtonKind = kpbkRollUp
    Caption = 'TeHeaderPanel2'
    CaptionHeight = 20
    LinkPanels = False
    Rolled = False
    ParentRoll = False
    ShowBevel = False
    ShowButton = False
    ShowCaption = False
    ThemeObject = 'default'
    NormalHeight = {00000000}
    object TeScaleTree1: TTeScaleTree
      Left = 0
      Top = 0
      Width = 360
      Height = 385
      Align = alTop
      Indent = 19
      ReadOnly = True
      TabOrder = 0
      Items.Data = {
        040000001E0000000000000000000000FFFFFFFFFFFFFFFF0000000007000000
        054D616A6F72230000000000000000000000FFFFFFFFFFFFFFFF000000000000
        00000A31202D20496F6E69616E230000000000000000000000FFFFFFFFFFFFFF
        FF00000000000000000A32202D20446F7269616E250000000000000000000000
        FFFFFFFFFFFFFFFF00000000000000000C33202D20506872796769616E230000
        000000000000000000FFFFFFFFFFFFFFFF00000000000000000A34202D204C79
        6469616E270000000000000000000000FFFFFFFFFFFFFFFF0000000000000000
        0E35202D204D79786F6C696469616E240000000000000000000000FFFFFFFFFF
        FFFFFF00000000000000000B36202D2041656F6C69616E240000000000000000
        000000FFFFFFFFFFFFFFFF00000000000000000B37202D204C6F637269616E26
        0000000000000000000000FFFFFFFFFFFFFFFF00000000070000000D4D656C6F
        646963206D696E6F72270000000000000000000000FFFFFFFFFFFFFFFF000000
        00000000000E31202D204A617A7A206D696E6F72260000000000000000000000
        FFFFFFFFFFFFFFFF00000000000000000D32202D20446F7269616E2062322D00
        00000000000000000000FFFFFFFFFFFFFFFF00000000000000001433202D204C
        796469616E206175676D656E7465642C0000000000000000000000FFFFFFFFFF
        FFFFFF00000000000000001334202D204C796469616E20646F6D696E616E742A
        0000000000000000000000FFFFFFFFFFFFFFFF00000000000000001135202D20
        4D69786F6C796469616E206236270000000000000000000000FFFFFFFFFFFFFF
        FF00000000000000000E36202D204C6F637269616E2023322A00000000000000
        00000000FFFFFFFFFFFFFFFF00000000000000001137202D205375706572206C
        6F637269616E270000000000000000000000FFFFFFFFFFFFFFFF000000000700
        00000E4861726D6F6E6963206D696E6F722B0000000000000000000000FFFFFF
        FFFFFFFFFF00000000000000001231202D204861726D6F6E6963206D696E6F72
        2B0000000000000000000000FFFFFFFFFFFFFFFF00000000000000001232202D
        204C6F637269616E206E61742E2036250000000000000000000000FFFFFFFFFF
        FFFFFF00000000000000000C33202D204D616A6F722023352600000000000000
        00000000FFFFFFFFFFFFFFFF00000000000000000D34202D20446F7269616E20
        23342E0000000000000000000000FFFFFFFFFFFFFFFF00000000000000001535
        202D20506872796769616E2D646F6D696E616E74260000000000000000000000
        FFFFFFFFFFFFFFFF00000000000000000D36202D204C796469616E2023322B00
        00000000000000000000FFFFFFFFFFFFFFFF00000000000000001237202D204C
        6F637269616E20623420626237270000000000000000000000FFFFFFFFFFFFFF
        FF00000000070000000E4861726D6F6E6963206D616A6F722B00000000000000
        00000000FFFFFFFFFFFFFFFF00000000000000001231202D204861726D6F6E69
        63206D616A6F72260000000000000000000000FFFFFFFFFFFFFFFF0000000000
        0000000D32202D20446F7269616E206235280000000000000000000000FFFFFF
        FFFFFFFFFF00000000000000000F33202D20506872796769616E206234260000
        000000000000000000FFFFFFFFFFFFFFFF00000000000000000D34202D204C79
        6469616E2062332A0000000000000000000000FFFFFFFFFFFFFFFF0000000000
        0000001135202D204D69786F6C796469616E2062323000000000000000000000
        00FFFFFFFFFFFFFFFF00000000000000001736202D204C796469616E20617567
        6D656E746564202332280000000000000000000000FFFFFFFFFFFFFFFF000000
        00000000000F37202D204C6F637269616E20626237}
      ThemeObject = 'default'
    end
  end
end
