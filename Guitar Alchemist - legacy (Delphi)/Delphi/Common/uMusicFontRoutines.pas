unit uMusicFontRoutines;

interface

uses
   uMusicClasses
   ;

const

   STAFF_FONT_NAME = 'guitalc1';
   CHORDS_FONT_NAME = 'guitalc2';


   // Staff font characters

   TAB_CHARACTER =             #$3A;
   G_KEY_CHARACTER =           #$27;
   SHARP_CHARACTER =           #$A9;
   DOUBLE_SHARP_CHARACTER =    #$AA;
   FLAT_CHARACTER =            #$A6;
   DOUBLE_FLAT_CHARACTER =     #$A7;
   NATURAL_CHARACTER =         #$A8;

   WHOLE_NOTE_CHARACTER =      #$9B;
   HALF_NOTE_CHARACTER =       #$9C;

   MUTED_POSITION =            #$3C;
   OPEN_POSITION =             #$2C;
   NORMAL_POSITION =           #$3E;
   NORMAL_POSITION_R =         #$7C;   
   NORMAL_POSITION_SMALL =     #$3F;
   NORMAL_POSITION_TINY =      #$40;
   CHARACTER_TONE_PRIMARY =    #$BD;
   CHARACTER_TONE_SECONDARY =  #$BE;

   DEGREE_QUALITY_1 =          #$CC;
   DEGREE_QUALITY_b2 =         #$CD;
   DEGREE_QUALITY_2 =          #$CE;
   DEGREE_QUALITY_b3 =         #$CF;
   DEGREE_QUALITY_3 =          #$D0;
   DEGREE_QUALITY_4 =          #$D1;
   DEGREE_QUALITY_b5 =         #$D2;
   DEGREE_QUALITY_5 =          #$D3;
   DEGREE_QUALITY_Sharp5 =     #$D4;
   DEGREE_QUALITY_6 =          #$D5;
   DEGREE_QUALITY_b7 =         #$D6;
   DEGREE_QUALITY_7 =          #$D7;
   DEGREE_QUALITY_8 =          #$D8;
   DEGREE_QUALITY_b9 =         #$D9;
   DEGREE_QUALITY_9 =          #$DA;
   DEGREE_QUALITY_Sharp9 =     #$DB;
   DEGREE_QUALITY_10 =         #$DC;
   DEGREE_QUALITY_11 =         #$DD;
   DEGREE_QUALITY_Sharp11 =    #$DE;
   DEGREE_QUALITY_12 =         #$DF;
   DEGREE_QUALITY_b13 =        #$E0;
   DEGREE_QUALITY_13 =         #$E1;
   DEGREE_QUALITY_bb7 =        #$E2;

   ACCIDENTAL_CHARACTERS : array[TNoteAccidental] of char =
      (#32, DOUBLE_FLAT_CHARACTER, FLAT_CHARACTER, NATURAL_CHARACTER, SHARP_CHARACTER, DOUBLE_SHARP_CHARACTER);

   // Chord font characters
   MINOR_CHARACTER =           #$69;

   KEY_FLAT =                  #$C6;
   KEY_SHARP =                 #$C7;

   UNISON_CHAR =               #$75;
   MINOR_SECOND_CHAR =         #$46;
   MAJOR_SECOND_CHAR =         #$45;
   MINOR_THIRD_CHAR =          #$4A;
   MAJOR_THIRD_CHAR =          #$49;
   PERFECT_FOURTH_CHAR =       #$4D;
   SHARP_FOURTH_CHAR =         #$4F;
   DIMINISHED_FIFTH_CHAR =     #$52;
   PERFECT_FIFTH_CHAR =        #$51;
   AUGMENTED_FIFTH_CHAR =      #$53;
   MINOR_SIXTH_CHAR =          #$56;
   MAJOR_SIXTH_CHAR =          #$55;
   DIM_SEVENTH_CHAR =          #$5C;
   MINOR_SEVENTH_CHAR =        #$5A;
   MAJOR_SEVENTH_CHAR =        #$59;
   OCTAVE_CHAR =               #$5D;
   MINOR_NINTH_CHAR =          #$62;
   MAJOR_NINTH_CHAR =          #$61;
   AUGMENTED_NINTH_CHAR =      #$63;
   MAJOR_TENTH_CHAR =          #$65;
   PERFECT_ELEVENTH_CHAR =     #$69;
   AUGMENTED_ELEVENTH_CHAR =   #$6B;
   PERFECT_TWELFTH_CHAR =      #$6D;
   AUGMENTED_TWELFTH_CHAR =    #$74;
   MINOR_THIRTEENTH_CHAR =     #$72;
   THIRTEENTH_CHAR =           #$71;

   FIVE_CHORD_CHAR =             #$35;
   SIX_CHORD_CHAR =              #$36;
   SIX_SEVEN_CHORD_CHAR =        #$26;
   SIX_NINE_CHORD_CHAR =         #$30;
   SEVEN_CHORD_CHAR =            #$37;
   FLAT_NINE_CHORD_CHAR =        #$38;
   NINE_CHORD_CHAR =             #$39;
   SHARP_NINE_CHORD_CHAR=        #$3A;
   FLAT_FIVE_CHORD_CHAR =        #$31;
   SHARP_FIVE_CHORD_CHAR =       #$33;
   ELEVEN_CHORD_CHAR =           #$3B;
   SHARP_ELEVEN_CHORD_CHAR =     #$3C;
   NATURAL_ELEVENTH_CHORD_CHAR = #$3D;
   FLAT_THIRTEEN_CHORD_CHAR =    #$3E;
   THIRTEEN_CHORD_CHAR =         #$3F;


   SUS_CHORD_CHAR =              #$6B;
   SUS2_CHORD_CHAR =             #$6C;
   SUS4_CHORD_CHAR =             #$6D;
   MIN_CHORD_CHAR =              #$69;
   MAJ_CHORD_CHAR =              #$6E;
   DIM_CHORD_CHAR =              #$64;
   HALF_DIM_CHORD_CHAR =         #$67;
   AUG_CHORD_CHAR =              #$68;
   DOM_CHORD_CHAR =              #$63;

   SLASH_CHORD_CHAR =            #$2F;
   ADD_CHORD_CHAR =              #$71;
   LEFT_BRACKET_CHORD_CHAR =     #$28;
   RIGHT_BRACKET_CHORD_CHAR =    #$29;

   UNKNOWN_CHAR =                #$70;

   FINGER1_CHAR =                #$84;

   VERT_BARRE1_CHAR =            #$88;
   HORIZ_BARRE1_CHAR =           #$8D;

   FRET_MARKER_DOT =             #$92;
   FRET_MARKER_DOUBLE_DOT =      #$93;
   FRET_MARKER_STAR =            #$94;

   DOUBLE_CHEVRON_LEFT =         #$C8;
   SIMPLE_CHEVRON_LEFT =         #$C9;
   DOUBLE_CHEVRON_RIGHT =        #$CA;
   SIMPLE_CHEVRON_RIGHT =        #$CB;

   CURSOR_FOCUSED  =             #$FE;
   CURSOR_SELECTED =             #$FE;

   FIRST_QUALITY_CHAR =          #$AB;
   QUALITY_DIMINISHED =          #$B8;
   QUALITY_FLAT =                #$B9;
   QUALITY_SHARP =               #$BA;
   QUALITY_AUGMENTED =           #$BB;

   QUALITY_ACCIDENTAL_CHARACTERS : array[TNoteAccidental] of char =
      (#32, QUALITY_DIMINISHED, QUALITY_FLAT, #32, QUALITY_SHARP, QUALITY_AUGMENTED);

   function GetChordNumericRomanChar(value : integer) : char;

   function GetChordQualityChar(quality : THalfToneQuality) : char;

   function GetToneQualityChar(quality : TToneQuality; alteration : TNoteAccidental; rForRoot : boolean = false) : char;
   function GetHalfToneQualityChar(quality : THalfToneQuality; rForRoot : boolean = false) : char;

   function GetHorizBarreChar(extent : integer) : char;
   function GetVertBarreChar(extent : integer) : char;

   function AreFontInstalled : boolean;


implementation

// {$R spfonts.res}

uses
   Classes
   ,SysUtils
   ,Windows
   ,Messages
   ;

   function GetChordNumericRomanChar(value : integer) : char;
   begin
      if value < 0
         then value := 0
      else if value > 6
         then value := 6;
      result := Chr($BF + value);
   end;

function GetChordQualityChar(quality : THalfToneQuality) : char;
   const
      DEGREE_QUALITITES : array [THalfToneQuality] of char =
      (
         DEGREE_QUALITY_1,
         DEGREE_QUALITY_b2,
         DEGREE_QUALITY_2,
         DEGREE_QUALITY_b3,
         DEGREE_QUALITY_3,
         DEGREE_QUALITY_4,
         DEGREE_QUALITY_b5,
         DEGREE_QUALITY_5,
         DEGREE_QUALITY_Sharp5,
         DEGREE_QUALITY_6,
         DEGREE_QUALITY_b7,
         DEGREE_QUALITY_7,
         DEGREE_QUALITY_8,
         DEGREE_QUALITY_b9,
         DEGREE_QUALITY_9,
         DEGREE_QUALITY_Sharp9,
         DEGREE_QUALITY_10,
         DEGREE_QUALITY_11,
         DEGREE_QUALITY_Sharp11,
         DEGREE_QUALITY_12,
         DEGREE_QUALITY_b13,
         DEGREE_QUALITY_13
      );
begin
   result := DEGREE_QUALITITES[quality];
end;

function GetToneQualityChar(quality : TToneQuality; alteration : TNoteAccidental; rForRoot : boolean) : char;
const
   TONE_QUALITY_CHARS : array[TToneQuality] of char =
   (
      NORMAL_POSITION, MAJOR_SECOND_CHAR, MAJOR_THIRD_CHAR, PERFECT_FOURTH_CHAR, PERFECT_FIFTH_CHAR,
      MAJOR_SIXTH_CHAR, MAJOR_SEVENTH_CHAR, OCTAVE_CHAR, MAJOR_NINTH_CHAR, MAJOR_TENTH_CHAR,
      PERFECT_ELEVENTH_CHAR, PERFECT_TWELFTH_CHAR, THIRTEENTH_CHAR
   );


   TONE_QUALITY_CHARS_R : array[TToneQuality] of char =
   (
      NORMAL_POSITION_R, MAJOR_SECOND_CHAR, MAJOR_THIRD_CHAR, PERFECT_FOURTH_CHAR, PERFECT_FIFTH_CHAR,
      MAJOR_SIXTH_CHAR, MAJOR_SEVENTH_CHAR, OCTAVE_CHAR, MAJOR_NINTH_CHAR, MAJOR_TENTH_CHAR,
      PERFECT_ELEVENTH_CHAR, PERFECT_TWELFTH_CHAR, THIRTEENTH_CHAR
   );
begin
   if rForRoot
      then result := TONE_QUALITY_CHARS_R[quality]
      else result := TONE_QUALITY_CHARS[quality];
   case alteration of
      naDoubleFlat: result := Chr(Ord(result) + 3);
      naFlat: result := Chr(Ord(result) + 1);
      naSharp: result := Chr(Ord(result) + 2);
      naDoubleSharp: Assert(false, 'Double sharp char not handled');
   end;
end;

function GetHalfToneQualityChar(quality : THalfToneQuality; rForRoot : boolean = false) : char;
const
   HALFTONE_QUALITY_CHARS : array[THalfToneQuality] of char =
   (
      NORMAL_POSITION, MINOR_SECOND_CHAR, MAJOR_SECOND_CHAR, MINOR_THIRD_CHAR, MAJOR_THIRD_CHAR,
      PERFECT_FOURTH_CHAR, DIMINISHED_FIFTH_CHAR, PERFECT_FIFTH_CHAR, AUGMENTED_FIFTH_CHAR,
      MAJOR_SIXTH_CHAR, MINOR_SEVENTH_CHAR, MAJOR_SEVENTH_CHAR, OCTAVE_CHAR, MINOR_NINTH_CHAR,
      MAJOR_NINTH_CHAR, AUGMENTED_NINTH_CHAR, MAJOR_TENTH_CHAR, PERFECT_ELEVENTH_CHAR,
      AUGMENTED_ELEVENTH_CHAR, PERFECT_TWELFTH_CHAR, MINOR_THIRTEENTH_CHAR, THIRTEENTH_CHAR
   );


   HALFTONE_QUALITY_CHARS_R : array[THalfToneQuality] of char =
   (
      NORMAL_POSITION_R, MINOR_SECOND_CHAR, MAJOR_SECOND_CHAR, MINOR_THIRD_CHAR, MAJOR_THIRD_CHAR,
      PERFECT_FOURTH_CHAR, DIMINISHED_FIFTH_CHAR, PERFECT_FIFTH_CHAR, AUGMENTED_FIFTH_CHAR,
      MAJOR_SIXTH_CHAR, MINOR_SEVENTH_CHAR, MAJOR_SEVENTH_CHAR, OCTAVE_CHAR, MINOR_NINTH_CHAR,
      MAJOR_NINTH_CHAR, AUGMENTED_NINTH_CHAR, MAJOR_TENTH_CHAR, PERFECT_ELEVENTH_CHAR,
      AUGMENTED_ELEVENTH_CHAR, PERFECT_TWELFTH_CHAR, MINOR_THIRTEENTH_CHAR, THIRTEENTH_CHAR
   );
begin
   if rForRoot
      then result := HALFTONE_QUALITY_CHARS_R[quality]
      else result := HALFTONE_QUALITY_CHARS[quality];
end;

function GetHorizBarreChar(extent : integer) : char;
begin
   if (extent > 1) and (extent < 7)
      then result := Chr(Ord(HORIZ_BARRE1_CHAR) + 6 - extent)
      else result := #32;
end;

function GetVertBarreChar(extent : integer) : char;
begin
   if (extent > 1) and (extent < 7)
      then result := Chr(Ord(VERT_BARRE1_CHAR) + 6 - extent)
      else result := #32;
end;

function GetFontName(fotFilename : string) : string;
var
   hFile : file;
   Buffer : string;
   iPos, jPos : Integer;
begin
   if FileExists(fotFilename)
      then begin
              AssignFile(hFile, fotFilename);
              Reset(hFile, 1);
              SetLength(Buffer, FileSize(hFile));
              BlockRead(hFile, Buffer[1], FileSize(hFile));

              // The name sits behind the text 'FONTRES:'
              iPos := Pos('FONTRES:', Buffer) + 8;

              // Search for next null character }
              jPos := iPos;
              while Buffer[jPos] <> #0
                 do Inc(jPos);

              // Return the font name
              Result := Copy(Buffer, iPos, jPos - iPos);

              // Clean up
              CloseFile(hFile);
           end
      else result := '';
end;

function EnumFontsProc(var logFont : TLogFont; var textMetric : TTextMetric; fontType : Integer; data : Pointer) : integer; stdcall;
var
   s : TStrings;
   temp : string;
begin
  s := TStrings(Data);
  temp := LogFont.lfFaceName;
  if (s.count = 0) or (AnsiCompareText(s[s.count-1], temp) <> 0)
     then s.Add(Temp);
  result := 1;
end;

procedure GetInstalledFonts(fonts : TStrings);
var
  strings : TStrings;
  dc : HDC;
  lFont : TLogFont;
begin
  if Assigned(fonts)
     then begin
             dc := GetDC(0);
             strings := TStringList.Create;
             try
                fonts.Add('Default');
                FillChar(lFont, sizeof(LFont), 0);
                lFont.lfCharset := DEFAULT_CHARSET;
                EnumFontFamiliesEx(dc, lFont, @EnumFontsProc, LongInt(strings), 0);
                TStringList(strings).sorted := true;
                fonts.Assign(strings);
             finally
                ReleaseDC(0, dc);
                strings.Free;
             end;
          end;
end;

procedure MaybeInstallFonts(fontResourceNames : array of string);
var
   installedFonts : TStringList;
   newFontsInstalled : boolean;
   index : integer;
   fontResourceName : string;
   ttfFilename : string;
   fotFilename : string;
   fontName : string;   
   res : TResourceStream;
begin
   // Inits
   installedFonts := TStringList.Create;
   try
      GetInstalledFonts(installedFonts);
      newFontsInstalled := false;

      // Check and install fonts
      for index := 0 to High(fontResourceNames) do
         begin
            fontResourceName := fontResourceNames[index];
            ttfFilename := ExtractFilePath(ParamStr(0)) + fontResourceName + '.ttf';

            // Save font file to disk
            res := TResourceStream.Create(hInstance, fontResourceName, RT_RCDATA);
            try
               res.SavetoFile(ttfFilename);
            finally
               res.Free;
            end;

            // Create fot file
            fotFilename := ChangeFileExt(ttfFilename, '.fot');
            CreateScalableFontResource(0, PChar(fotFilename), PChar(ttfFilename), nil);
            fontName := GetFontName(fotFilename);

            // Maybe install the font
            if installedFonts.IndexOf(fontName) = -1
               then begin
                       if AddFontResource(PChar(fotFilename)) > 0
                          then newFontsInstalled := true;
                    end;
            end;

   finally
      installedFonts.Free;
   end;

   // Notify font changes
   if newFontsInstalled
      then SendMessage(HWND_BROADCAST, WM_FONTCHANGE, 0, 0);
end;

procedure MaybeUninstallFonts(fontResourceNames : array of string);
var
   installedFonts : TStringList;
   fontsUninstalled : boolean;
   index : integer;
   fontResourceName : string;
   ttfFilename : string;
   fotFilename : string;
   fontName : string;   
begin
   // Inits
   installedFonts := TStringList.Create;
   try
      GetInstalledFonts(installedFonts);
      fontsUninstalled := false;

      // Check and install fonts
      for index := 0 to High(fontResourceNames) do
         begin
            fontResourceName := fontResourceNames[index];
            ttfFilename := ExtractFilePath(ParamStr(0)) + fontResourceName + '.ttf';

            // Create fot file
            fotFilename := ChangeFileExt(ttfFilename, '.fot');
            if not FileExists(fotFilename)
               then CreateScalableFontResource(0, PChar(fotFilename), PChar(ttfFilename), nil);
            fontName := GetFontName(fotFilename);

            // Maybe install the font
            if installedFonts.IndexOf(fontName) <> -1
               then begin
                       if RemoveFontResource(PChar(fotFilename))
                          then fontsUninstalled := true;
                    end;

            // Remove font files
            SysUtils.DeleteFile(ttfFilename);
            SysUtils.DeleteFile(fotFilename);
         end;

   finally
      installedFonts.Free;
   end;

   // Notify font changes
   if fontsUninstalled
      then SendMessage(HWND_BROADCAST, WM_FONTCHANGE, 0, 0);
end;

function AreFontInstalled : boolean;
var
   installedFonts : TStringList;
begin
   installedFonts := TStringList.Create;
   try
      GetInstalledFonts(installedFonts);
      result := (installedFonts.IndexOf(STAFF_FONT_NAME) <> -1)
                and (installedFonts.IndexOf(CHORDS_FONT_NAME) <> -1);
   finally
      installedFonts.Free;
   end;
end;



end.
