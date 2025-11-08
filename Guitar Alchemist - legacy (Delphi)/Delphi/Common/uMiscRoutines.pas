unit uMiscRoutines;

interface

uses
   Math
   ,Classes
   ,SysUtils
   ;


function GetFretPosition(fret : integer) : extended; overload;
function GetFretPosition(fret : extended; fretSpaceWidth : integer; fretCount : integer = 19) : integer; overload;
function GetPositionFret(position : integer; fretSpaceWidth : integer; fretCount : integer = 19) : integer;
function GetStringPosition(str : integer) : extended;
function GetFretsDistance(fret1, fret2 : integer) : extended;
function GetStringsDistance(str1, str2 : integer) : extended;
function GetFingerDistance(str1, fret1, str2, fret2 : integer) : extended;

function PlayWavefile(filename : string) : boolean;

implementation

uses
   Windows
   ,MMSystem
   ;

const
   GUITAR_NECK_LENGTH_IN_CM = 65;
var
   stringDistance : extended;


function GetFretPosition(fret : integer) : extended;
begin
   result := GUITAR_NECK_LENGTH_IN_CM * Power(0.5, fret/12);
end;

function GetFretPosition(fret : extended; fretSpaceWidth, fretCount : integer) : integer;
var
   guitarNeckLength : extended;
begin
   guitarNeckLength := fretSpaceWidth / (1 - Power(0.5, fretCount/12));
   result := Round(guitarNeckLength * (Power(0.5, fret/12) - Power(0.5, fretCount / 12)));
end;

function GetPositionFret(position : integer; fretSpaceWidth : integer; fretCount : integer = 19) : integer;
var
   guitarNeckLength : extended;
   x : extended;
begin
   guitarNeckLength := fretSpaceWidth / (1 - Power(0.5, fretCount/12));
   x := position / guitarNeckLength + Power(0.5, fretCount/12);
   result := Round(12 * LogN(0.5, x) + 0.5);
end;

function GetStringPosition(str : integer) : extended;
begin
   result := stringDistance * str;
end;

function GetFretsDistance(fret1, fret2 : integer) : extended;
begin
   result := Abs(GetFretPosition(fret2) - GetFretPosition(fret1));
end;

function GetStringsDistance(str1, str2 : integer) : extended;
begin
   result := Abs(GetStringPosition(str2) - GetStringPosition(str1));
end;

function GetFingerDistance(str1, fret1, str2, fret2 : integer) : extended;
begin
   result := Sqrt(IntPower(GetFretsDistance(fret1, fret2), 2) + IntPower(GetStringsDistance(str1, str2), 2));
end;

function PlayWavefile(filename : string) : boolean;
begin
   result := false;
   try
      // Stop any previous sound
      PlaySound(nil, 0, SND_ASYNC or SND_FILENAME or SND_NOWAIT);
      if FileExists(filename)
         then begin
                 PlaySound(pchar(filename), 0, SND_ASYNC or SND_FILENAME or SND_NOWAIT);
                 result := true;
              end
   except
   end;

end;

initialization
begin
   stringDistance := GetFretsDistance(19, 20);
end;



end.


