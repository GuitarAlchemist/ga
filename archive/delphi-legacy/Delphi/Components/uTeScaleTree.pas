unit uTeScaleTree;

interface

uses
   classes
   ,ComCtrls
   ,Graphics
   ,Types
   ,te_controls
   ,uMusicClasses
   ,uMusicFontRoutines
   ;


type
   TTeScaleTree = class(TTeTreeView)
      private
         fScale : TScale;
         fRequiredQualities : THalfToneQualities;
         procedure DoAdvancedCustomDrawItem(Sender: TCustomTreeView; Node: TTreeNode;
                                            State: TCustomDrawState; Stage: TCustomDrawStage; var PaintImages,
                                            DefaultDraw: Boolean);
         procedure SetRequiredQualities(value : THalfToneQualities);
      protected
         procedure Loaded; override;
         procedure CreateWnd; override;
         procedure Populate; virtual;
         function CanChange(node : TTreeNode) : boolean; override;
      public
         constructor Create(AOwner: TComponent); override;
         destructor Destroy; override;
         function SelectScale(scaleName : string; scaleMode : integer) : boolean;
      published
         property RequiredQualities : THalfToneQualities read fRequiredQualities write SetRequiredQualities;
   end;

implementation

uses
   SysUtils
   ,Windows
   ,Messages
   , Controls;

{ TTeScaleTree }

constructor TTeScaleTree.Create(AOwner: TComponent);
begin
   inherited Create(AOwner);
   fScale := TScale.Create;
   fRequiredQualities := [];
   ReadOnly := true;
   OnAdvancedCustomDrawItem := DoAdvancedCustomDrawItem;
end;

destructor TTeScaleTree.Destroy;
begin
   fScale.Free;
   inherited;
end;

function TTeScaleTree.SelectScale(scaleName : string; scaleMode : integer) : boolean;
var
   node : TTreeNode;
   index : integer;
begin
   result := false;
   node := items.GetFirstNode;
   repeat
      if SameText(node.text, scaleName)
         then // Found the scale
              begin
                 if node.HasChildren
                    then // Look for the mode
                         for index := 0 to node.count - 1 do
                            begin
                               Assert(node.item[index] <> nil);
                               if Integer(node.item[index].data) = scaleMode - 1
                                  then begin
                                          node.item[index].selected := true;
                                          result := true;
                                          Break;
                                       end;
                            end
                    else // No modes for this scale
                         begin
                            node.selected := true;
                            result := true;
                         end;
              end;
      node := node.getNextSibling;
   until (node = nil) or result;
end;

procedure TTeScaleTree.Populate;
var
   scaleNames : TStringList;
   modeNames : TStringList;
   scale : TScale;
   scaleIndex : integer;
   modeIndex : integer;
   scaleName : string;
   modeName : string;
   scaleNode : TTreeNode;
   modeNode : TTreeNode;
   requiredQualities : THalfToneQualities;
begin
   requiredQualities := fRequiredQualities;
   LimitQualitiesToFirstOctave(requiredQualities);

   scaleNames := TStringList.Create;
   modeNames := TStringList.Create;
   scale := TScale.Create;
   Items.BeginUpdate;
   try
      // Clear the existing tree
      Items.Clear;

      // Add scale name nodes
      globalScaleRepository.GetNames(scaleNames);
      for scaleIndex := 0 to scaleNames.count - 1 do
         begin
            scaleNode := nil;
            scaleName := LowerCase(scaleNames[scaleIndex]);
{$WARNINGS OFF}
            if scaleName <> ''
               then scaleName[1] := UpCase(scaleName[1]);
{$WARNINGS ON}
            if globalScaleRepository.IsScaleVisible(scaleName)
               then begin
                       if globalScaleRepository.AreModeAllowed(scaleName)
                          then // Scale and modes
                               begin
                                  globalScaleRepository.GetModes(scaleName, modeNames, true);
                                  for modeIndex := 0 to modeNames.count - 1 do
                                     begin
                                        modeName := modeNames[modeIndex];
                                        if // No qualities required
                                           (requiredQualities = [])
                                           // Only add scales or modes that contain the required qualities
                                           or ( globalScaleRepository.GetScale(scale, scaleName, modeIndex + 1)
                                                and scale.ContainsQualities(requiredQualities)
                                              )
                                           then
                                                begin
                                                   // Add the parent node if needed
                                                   if scaleNode = nil
                                                      then scaleNode := Items.AddChild(nil, scaleName);

                                                   // Add the mode
                                                   modeNode := Items.AddChild(scaleNode, modeName);
                                                   modeNode.data := Pointer(modeIndex);
                                                end
                                     end;
                               end
                          else // Single scale
                               begin
                                  if // No qualities required
                                     (requiredQualities = [])
                                     // Only add scales or modes that contain the required qualities
                                     or ( globalScaleRepository.GetScale(scale, scaleName)
                                          and scale.ContainsQualities(requiredQualities)
                                        )
                                           then begin
                                                   modeNode := Items.AddChild(nil, scaleName);
                                                   modeNode.data := Pointer(0);
                                                end;
                               end;
                    end;
         end;
   finally
      Items.EndUpdate;
      scaleNames.Free;
      modeNames.Free;
      scale.Free;
   end;

   // Maybe automatically expand the tree
   if fRequiredQualities <> []
      then begin
              if items.count > 0
                 then begin
                         FullExpand;
                         topItem := items[0];

                         // Automatically select the first scale or mode
                         if items[0].HasChildren
                            then items[0].getFirstChild.selected := true
                            else items[0].selected := true;

                         // Simulate key strike
                         if not(csLoading in componentState)
                            then SendMessage(handle, WM_KEYUP, 0, 0);
                      end;
           end
{$IFDEF FreeVersion}
      else begin
              if items.count > 0
                 then begin
                         FullExpand;
                         topItem := items[0];

                         // Automatically select the first scale or mode
                         if items[0].HasChildren
                            then items[0].getFirstChild.selected := true
                            else items[0].selected := true;
                      end;
           end;
{$ENDIF}

end;

function TTeScaleTree.CanChange(node : TTreeNode) : boolean;
begin
   result := (fRequiredQualities = []) // Can always select the parent scale if not filtered
             or (node.parent <> nil) // The node is a mode
             or (not node.HasChildren); // or the node is a scale that does not have any mode
end;

procedure TTeScaleTree.DoAdvancedCustomDrawItem(Sender: TCustomTreeView; Node: TTreeNode;
                                                State: TCustomDrawState; Stage: TCustomDrawStage; var PaintImages,
                                                DefaultDraw: Boolean);

var
   scaleName : string;
   mode : integer;
   rect : TRect;
   index : integer;
   rootNote : shortint;
   diatonicRootNote : shortint;
   degreeNote : smallint;
   degreeQuality : smallint;
   degreeAccidental : smallint;
  
   ch : char;
   x, y : integer;
begin
   if stage = cdPostPaint
      then begin
              if node.parent <> nil
                 then begin
                          scaleName := node.parent.text;
                          if fRequiredQualities <> []
                             then mode := integer(node.data)
                             else mode := node.index;
                      end
                 else begin
                         if node.HasChildren
                            then Exit;
                         scaleName := node.text;
                         mode := 0;
                      end;

              // Draw the scale formula
              globalScaleRepository.GetScale(fScale, scaleName);
              rootNote := 0;
              for index := 0 to mode - 1 do
                 rootNote := (rootNote + fScale.degreeInterval[index]) mod 12;
              diatonicRootNote := DIATONIC_TONE[rootNote];
              degreeNote := rootNote;

              rect := node.DisplayRect(true);
              canvas.brush.color := brush.color;
              canvas.font.color := font.color;
              x := 170;
              y := rect.top;

              canvas.font.name := STAFF_FONT_NAME;
              canvas.font.size := 16;

              for index := 0 to fScale.count do
                 begin
                    // Compute the quality for the current degree
                    degreeQuality := (DIATONIC_TONE[degreeNote] - diatonicRootNote + 7) mod 7;
                    degreeAccidental := DEGREE_ACCIDENTAL[degreeNote] - KEY_ACCIDENTAL[rootNote, DIATONIC_TONE[degreeNote]];

                    // Draw the degree accidental
                    if degreeAccidental <> 0
                       then begin
                               ch := #0;
                               rect.left := x - 4;
                               rect.right := x + 4;
                               case degreeAccidental of
                                  -2: begin
                                         ch := QUALITY_DIMINISHED;
                                         rect.left := x - 8;
                                      end;
                                  -1: ch := QUALITY_FLAT;
                                  1: ch := QUALITY_SHARP;
                                  2: ch := QUALITY_AUGMENTED;
                                  else Assert(false, 'Invalid accidental');
                               end;
                               canvas.TextRect(rect, rect.left, y - 14, ch);
                            end;

                    // Draw the degree quality
                    ch := Chr(Ord(FIRST_QUALITY_CHAR) + degreeQuality);
                    rect.left := x;
                    rect.right := x + 12;
                    canvas.TextRect(rect, x, y - 14, ch);

                    // Pick up next degree note
                    degreeNote := (degreeNote + fScale.degreeInterval[mode + index]) mod 12;
                    Inc(x, 18);
                 end;
           end;
end;

procedure TTeScaleTree.Loaded;
begin
   inherited;
   Populate;
end;

procedure TTeScaleTree.CreateWnd;
begin
   inherited;
   Populate;
end;

procedure TTeScaleTree.SetRequiredQualities(value : THalfToneQualities);
begin
   if value <> fRequiredQualities
      then begin
              fRequiredQualities := value;
              Populate;
              Invalidate;
           end;
end;

end.
