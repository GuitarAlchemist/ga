unit uUpdatesCheck;

interface

uses
   Sockets
   ;

type
   TUpdatesChecker = class
     private
        fTcpClient : TTcpClient;
     public
        constructor Create;
        destructor Destroy; override;
        procedure CheckUpdates;  
   end;

implementation

const
   GUITAR_ALCHEMIST_HOST = 'guitaralchemist.com';
   GUITAR_ALCHEMIST_PORT = 80; // HTTP server

{ TUdatesChecker }

constructor TUdatesChecker.Create;
begin
   fTcpClient := TTcpClient.Create(nil);
   fTcpClient.remoteHost := GUITAR_ALCHEMIST_HOST;
   fTcpClient.RemotePort := GUITAR_ALCHEMIST_PORT;
end;

destructor TUdatesChecker.Destroy;
begin
   fTcpClient.Free;
   inherited;
end;

initialization
begin
end;

finalization
begin
//   globalScaleRepository.Free;
end;



end.