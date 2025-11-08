unit uArmadillo;

interface

uses
   SysUtils
   ;

   Function CheckCode (name,code:PChar) : Boolean; stdcall; external 'armaccess.dll';
   Function VerifyKey (name,code:PChar) : Boolean; stdcall; external 'armaccess.dll';
   Function InstallKey (name,code:PChar) : Boolean; stdcall; external 'armaccess.dll';
   Function InstallKeyLater (name,code:PChar) : Boolean; stdcall; external 'armaccess.dll';
   Function UninstallKey : Boolean; stdcall; external 'armaccess.dll';
   Function SetDefaultKey : Boolean; stdcall; external 'armaccess.dll';
   Function UpdateEnvironment : Boolean; stdcall; external 'armaccess.dll';
   Function IncrementCounter : Boolean; stdcall; external 'armaccess.dll';
   Function CopiesRunning : LongInt; stdcall; external 'armaccess.dll';
   Function ChangeHardwareLock : Boolean; stdcall; external 'armaccess.dll';
   Function GetShellProcessID : LongInt; stdcall; external 'armaccess.dll';
   Function FixClock (fixclockkey:PChar) : Boolean; stdcall; external 'armaccess.dll';
   Function RawFingerprintInfo (item:LongInt) : LongInt; stdcall; external 'armaccess.dll';
   Function CallBuyNowURL(parent:LongInt) : Boolean; stdcall; external 'armaccess.dll';

   function IsExpired : boolean;
   function IsClockback : boolean;
   function IsRegistered : boolean;
   function IsTrial : boolean; // Note that this is true for trial even if expired
   function IsBeta : boolean;
   function IsActiveTrial : boolean; // We're in trial mode and NOT expired
   function GetUserName : string;
   function GetUserKey : string;
   function GetDaysLeft : integer;
   function GetExpireEver : extended;

   function EnvVerExists(envvar : string) : boolean;

implementation

function EnvVerExists(envvar : string) : boolean;
begin
   UpdateEnvironment;
   result := GetEnvironmentVariable(envVar) <> ''
end;

function Match(const envVar : string; const value : string) : boolean;
begin
   UpdateEnvironment;
   result := GetEnvironmentVariable(envVar) = value
end;

function IsTrial : boolean;
begin
   result := Match('TRIAL', 'YES')
end;

function IsBeta : boolean;
begin
   result := Match('BETA', 'YES')
end;

function IsActiveTrial : boolean;
begin
   result := IsTrial and (not IsExpired);
end;

function IsExpired : boolean;
begin
   UpdateEnvironment;
   result := GetEnvironmentVariable('EXPIRED') <> '';
end;

function IsRegistered : boolean;
begin
   UpdateEnvironment;
   result := GetEnvironmentVariable('REGISTERED') = 'YES';
end;

function IsClockback : boolean;
begin
   UpdateEnvironment;
   result := GetEnvironmentVariable('CLOCKBACK') <> '';
end;

function GetUserName : string;
var
   s : string;
begin
   UpdateEnvironment;
   s := GetEnvironmentVariable('USERNAME');

   if s = 'DEFAULT'
      then s := '';
   result := s;
end;

function GetUserKey : string;
var
   s : string;
begin
   UpdateEnvironment;
   s := GetEnvironmentVariable('USERKEY');
   result := s;
end;

function GetDaysLeft : integer;
var
   s : string;
begin
   UpdateEnvironment;
   s := GetEnvironmentVariable('DAYSLEFT');
   result := StrToIntDef(s, -1);
end;

function GetExpireEver : extended;
var
   s : string;
begin
   UpdateEnvironment;
   s := GetEnvironmentVariable('EXPIREEVER');
   result := StrToFloatDef(s, 0);
end;


end.
