unit uVersionInformation;

interface

function GetMajorVersion : integer;
function GetMinorVersion : integer;
function GetVersionAsString : string;
function GetProductName : string;
function GetProductNameShort : string;
function GetProductNameAndVersion : string;
procedure UnavailableMessage(s : string = '');
procedure FatalError(s : string);

implementation

uses
   SysUtils
   ,Dialogs
   ,te_controls
   ;

const
// Version include
{$include version.inc}
// Do not remove !!!

{$if defined(FreeVersion)}
   // *** Free version ***
   ProductName = 'Guitar Alchemist Free';
   ProductNameShort = 'GAF';

{$elseif defined(GoldVersion)}
   // *** Gold version ***
   ProductName = 'Guitar Alchemist Gold' ;
   ProductNameShort = 'GAG';
   {$define CustomThemes} // Allow using custom themes in gold version only

{$else}

   // *** Regular version ***
   ProductName = 'Guitar Alchemist';
   ProductNameShort = 'GA';   

{$ifend}


function GetProductName : string;
begin
   result := ProductName;
end;

function GetProductNameShort : string;
begin
   result := ProductNameShort;
end;

function GetMajorVersion : integer;
begin
   result := MajorVersion;
end;

function GetMinorVersion : integer;
begin
   result := MinorVersion;
end;

function GetVersionAsString : string;
begin
   result := IntToStr(MajorVersion) + '.' + IntToStr(MinorVersion);
end;

function GetProductNameAndVersion : string;
begin
   result := GetProductName + ' ' + GetVersionAsString;
end;

procedure UnavailableMessage(s : string);
var
   teMsg : TTeMessage;
   msg : string;
begin
   teMsg := TTeMessage.Create(nil);
   try
      msg := 'This feature is not available in ' + GetProductName;
      if s <> ''
         then msg := msg + #13#10#13#10 + s;
      teMsg.MessageDlg(msg, mtInformation, [mbOK], 0);
   finally
      teMsg.Free;
   end;
end;

procedure FatalError(s : string);
var
   teMsg : TTeMessage;
begin
   teMsg := TTeMessage.Create(nil);
   try
      teMsg.MessageDlg(s, mtError, [mbOK], 0);
   finally
      teMsg.Free;
   end;
end;

end.
