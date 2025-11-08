unit uVoicingFingersFinder;

interface

uses
   uMusicClasses
   ,uChordVoicings
   ,Classes
   ;

type
   TFingersDifficulty = (fdEasy, fdMedium, fdHard, fdVeryHard, fdImpossible);

   TFingeringItem = class
      private
         fOrder : TVoicingFingersOrder;
         fScore : integer;
         fNatural : integer;
         fUseFourthFinger : boolean;
         fDifficulty : TFingersDifficulty;
      public
         property order : TVoicingFingersOrder read fOrder;
         property score : integer read fScore;
         property natural : integer read fNatural;
         property useFourthFinger : boolean read fUseFourthFinger;
         property difficulty : TFingersDifficulty read fDifficulty;
   end;

type
   TVoicingFingersFinder = class
      private
         fVoicing : TChordVoicing;
         fFormula : TChordVoicingFormula;
         fFingersOrders : TStrings;
         procedure SetVoicing(value : TChordVoicing);
         procedure MaybeAddItem(order : TVoicingFingersOrder);
      protected
         function GetItemScoreAndCaption(order : TVoicingFingersOrder; item : TFingeringItem; out caption : string) : boolean;
         procedure FindFingersOrder;
         procedure Clear;
      public
         constructor Create;
         destructor Destroy; override;
         property voicing : TChordVoicing read fVoicing write SetVoicing;
         property formula : TChordVoicingFormula read fFormula;
         property fingerOrders : TStrings read fFingersOrders;
   end;

implementation

uses
   math
   ,SysUtils
   ,uMiscRoutines
   ;


const
   FOUR_FINGERS_ORDER_TABLE : array[0..23] of TVoicingFingersOrder =
   (
      (1, 2, 3, 4),
      (1, 2, 4, 3),
      (1, 3, 2, 4),
      (1, 3, 4, 2),
      (1, 4, 2, 3),
      (1, 4, 3, 2),
      (2, 1, 3, 4),
      (2, 1, 4, 3),
      (2, 3, 1, 4),
      (2, 3, 4, 1),
      (2, 4, 1, 3),
      (2, 4, 3, 1),
      (3, 1, 2, 4),
      (3, 1, 4, 2),
      (3, 2, 1, 4),
      (3, 2, 4, 1),
      (3, 4, 1, 2),
      (3, 4, 2, 1),
      (4, 1, 2, 3),
      (4, 1, 3, 2),
      (4, 2, 1, 3),
      (4, 2, 3, 1),
      (4, 3, 1, 2),
      (4, 3, 2, 1)
   );

   THREE_FINGERS_ORDER_TABLE : array[0..23] of TVoicingFingersOrder =
   (
      (1, 2, 3, 0),
      (1, 2, 4, 0),
      (1, 3, 2, 0),
      (1, 3, 4, 0),
      (1, 4, 2, 0),
      (1, 4, 3, 0),
      (2, 1, 3, 0),
      (2, 1, 4, 0),
      (2, 3, 1, 0),
      (2, 3, 4, 0),
      (2, 4, 1, 0),
      (2, 4, 3, 0),
      (3, 1, 2, 0),
      (3, 1, 4, 0),
      (3, 2, 1, 0),
      (3, 2, 4, 0),
      (3, 4, 1, 0),
      (3, 4, 2, 0),
      (4, 1, 2, 0),
      (4, 1, 3, 0),
      (4, 2, 1, 0),
      (4, 2, 3, 0),
      (4, 3, 1, 0),
      (4, 3, 2, 0)
   );

   TWO_FINGERS_ORDER_TABLE : array[0..11] of TVoicingFingersOrder =
   (
      (1, 2, 0, 0),
      (1, 3, 0, 0),
      (1, 4, 0, 0),
      (2, 1, 0, 0),
      (2, 3, 0, 0),
      (2, 4, 0, 0),
      (3, 1, 0, 0),
      (3, 2, 0, 0),
      (3, 4, 0, 0),
      (4, 1, 0, 0),
      (4, 2, 0, 0),
      (4, 3, 0, 0)
   );

   ONE_FINGER_ORDER_TABLE : array[0..3] of TVoicingFingersOrder =
   (
      (1, 0, 0, 0),
      (2, 0, 0, 0),
      (3, 0, 0, 0),
      (4, 0, 0, 0)
   );


   F1BARRE_PLUS_THREE_FINGERS_1 : array [0..5] of TVoicingFingersOrder =
   (
   // >o< is barre
      (1, 2, 3, 4),
      (1, 2, 4, 3),
      (1, 3, 2, 4),
      (1, 3, 4, 2),
      (1, 4, 2, 3),
      (1, 4, 3, 2)
   );
   F1BARRE_PLUS_THREE_FINGERS_2 : array [0..5] of TVoicingFingersOrder =
   (
   //    >o< is barre
      (2, 1, 3, 4),
      (2, 1, 4, 3),
      (3, 1, 2, 4),
      (3, 1, 4, 2),
      (4, 1, 2, 3),
      (4, 1, 3, 2)
   );
   F1BARRE_PLUS_THREE_FINGERS_3 : array [0..5] of TVoicingFingersOrder =
   (
   //       >o< is barre
      (2, 3, 1, 4),
      (2, 4, 1, 3),
      (3, 2, 1, 4),
      (3, 4, 1, 2),
      (4, 2, 1, 3),
      (4, 3, 1, 2)
   );
   F1BARRE_PLUS_THREE_FINGERS_4 : array [0..5] of TVoicingFingersOrder =
   (
   //          >o< is barre
      (2, 3, 4, 1),
      (2, 4, 3, 1),
      (3, 2, 4, 1),
      (3, 4, 2, 1),
      (4, 2, 3, 1),
      (4, 3, 2, 1)
   );

   F1BARRE_PLUS_TWO_FINGERS_1 : array [0..5] of TVoicingFingersOrder =
   (
   // >o< is barre
      (1, 2, 3, 0),
      (1, 2, 4, 0),
      (1, 3, 2, 0),
      (1, 3, 4, 0),
      (1, 4, 2, 0),
      (1, 4, 3, 0)
   );
   F1BARRE_PLUS_TWO_FINGERS_2 : array [0..5] of TVoicingFingersOrder =
   (
   //    >o< is barre
      (2, 1, 3, 0),
      (2, 1, 4, 0),
      (3, 1, 2, 0),
      (3, 1, 4, 0),
      (4, 1, 2, 0),
      (4, 1, 3, 0)
   );
   F1BARRE_PLUS_TWO_FINGERS_3 : array [0..5] of TVoicingFingersOrder =
   (
   //       >o< is barre
      (2, 3, 1, 0),
      (2, 4, 1, 0),
      (3, 2, 1, 0),
      (3, 4, 1, 0),
      (4, 2, 1, 0),
      (4, 3, 1, 0)
   );

   F1BARRE_PLUS_ONE_FINGER_1 : array [0..2] of TVoicingFingersOrder =
   (
   // >o< is barre
      (1, 2, 0, 0),
      (1, 3, 0, 0),
      (1, 4, 0, 0)
   );

   F1BARRE_PLUS_ONE_FINGER_2 : array [0..2] of TVoicingFingersOrder =
   (
   //    >o< is barre
      (2, 1, 0, 0),
      (3, 1, 0, 0),
      (4, 1, 0, 0)
   );

   F1BARRE_WITHOUT_OTHER_FINGER : array [0..0] of TVoicingFingersOrder =
   (
      (1, 0, 0, 0)
   );


   MIN_FINGER_DISTANCES : array[0..3, 0..3] of extended =
   (
      (0, 0, 3, 4.5),
      (2, 0, 0, 2),
      (4, 1.5, 0, 0),
      (5, 3, 1.5, 0)
   );

   OPTIMUM_FINGER_DISTANCES : array[0..3, 0..3] of extended =
   (
      (0, 5.5, 8, 8.5),
      (5, 0, 4.5, 5.5),
      (5.5, 4.3, 0, 4.5),
      (8, 5.5, 4.3, 0)
   );

   MAX_FINGER_DISTANCES : array[0..3, 0..3] of extended =
   (
      (0, 7.5, 9.8, 14),
      (8, 0, 6.6, 7.5),
      (11, 8, 0, 7.2),
      (13.7, 8, 6.9, 0)
   );

   // Given a root position finger, gives which other fingers are permitted above/below the root position fret
   BELOW_FRET_FINGERS : array[0..3] of TFingers =
   ([], [1], [1, 2], [1, 2, 3]);

   ABOVE_FRET_FINGERS : array[0..3] of TFingers =
   ([2, 3, 4], [3, 4], [4], []);



function FingerOrderSort(list : TStringList; index1, index2 : integer) : integer;
var
   item1, item2 : TFingeringItem;

   function NaturalSort : integer;
   begin
           if item1.useFourthFinger > item2.useFourthFinger
              then result := 1
      else if item1.useFourthFinger < item2.useFourthFinger
              then result := -1
      else         result := 0;
   end;

   function ScoreSort : integer;
   begin
           if item1.score > item2.score
              then result := 1
      else if item1.score < item2.score
              then result := -1
      else         result := 0;
   end;

   function UseFourthFingerSort : integer;
   begin
           if item1.useFourthFinger < item2.useFourthFinger
              then result := 1
      else if item1.useFourthFinger > item2.useFourthFinger
              then result := -1
      else         result := 0;
   end;

begin
   // Inits
   Assert(list.objects[index1] is TFingeringItem);
   Assert(list.objects[index2] is TFingeringItem);
   item1 := TFingeringItem(list.objects[index1]);
   item2 := TFingeringItem(list.objects[index2]);

   // Sort
   result := NaturalSort;
   if result <> 0
      then Exit;

   result := ScoreSort;
   if result <> 0
      then Exit;

   result := UseFourthFingerSort;
end;

{ TVoicingFingersFinder }

constructor TVoicingFingersFinder.Create;
begin
   fVoicing := nil;
   fFingersOrders := TStringList.Create;
end;

destructor TVoicingFingersFinder.Destroy;
begin
   Clear;
   fFingersOrders.Free;
   inherited;
end;

procedure TVoicingFingersFinder.SetVoicing(value : TChordVoicing);
begin
   fVoicing := value;
   fFormula := voicing.formula;
   FindFingersOrder;
end;

procedure TVoicingFingersFinder.MaybeAddItem(order : TVoicingFingersOrder);
var
   item : TFingeringItem;
   caption : string;
begin
   item := TFingeringItem.Create;
   if GetItemScoreAndCaption(order, item, caption)
      then // Add the item to the list
           fFingersOrders.AddObject(Format('%s, s=%d, n=%d, 4=%d', [caption, item.score, item.natural, Ord(item.useFourthFinger)]), item)
      else // Delete the item
           item.Free;
end;

procedure TVoicingFingersFinder.FindFingersOrder;
var
   orderIndex : integer;
   firstFingerBarreOrder : integer;

   function GetBarreFingerOrder : integer;
   var
      str : integer;
      fret : integer;
      order : integer;
   begin
      order := 0;
      result := 0;
      for str := 0 to 5 do
         begin
            fret := voicing.positions[str].fret;
            if (str >= voicing.firstFingerBarreStartingStr - voicing.firstFingerBarreExtent)
               and (fret = voicing.minFret)
               then // Barre position
                    begin
                       result := order;
                       Exit;
                    end;
            if fret > 0
               then // Non muted or open position
                    Inc(order);
         end;
   end;


begin
   if voicing.firstFingerBarreExtent > 0
      then // First finger barre
           begin
              // Select the right order table
              firstFingerBarreOrder := GetBarreFingerOrder;
              case voicing.fingerCount of
                 4: case firstFingerBarreOrder of
                       0: for orderIndex := 0 to High(F1BARRE_PLUS_THREE_FINGERS_1) do
                             MaybeAddItem(F1BARRE_PLUS_THREE_FINGERS_1[orderIndex]);
                       1: for orderIndex := 0 to High(F1BARRE_PLUS_THREE_FINGERS_2) do
                             MaybeAddItem(F1BARRE_PLUS_THREE_FINGERS_2[orderIndex]);
                       2: for orderIndex := 0 to High(F1BARRE_PLUS_THREE_FINGERS_3) do
                             MaybeAddItem(F1BARRE_PLUS_THREE_FINGERS_3[orderIndex]);
                       3: for orderIndex := 0 to High(F1BARRE_PLUS_THREE_FINGERS_4) do
                             MaybeAddItem(F1BARRE_PLUS_THREE_FINGERS_4[orderIndex]);
                       else Assert(false, 'Fingers ordering search algorithm error');
                    end;
                 3: case firstFingerBarreOrder of
                       0: for orderIndex := 0 to High(F1BARRE_PLUS_TWO_FINGERS_1) do
                             MaybeAddItem(F1BARRE_PLUS_TWO_FINGERS_1[orderIndex]);
                       1: for orderIndex := 0 to High(F1BARRE_PLUS_TWO_FINGERS_2) do
                             MaybeAddItem(F1BARRE_PLUS_TWO_FINGERS_2[orderIndex]);
                       2: for orderIndex := 0 to High(F1BARRE_PLUS_TWO_FINGERS_3) do
                             MaybeAddItem(F1BARRE_PLUS_TWO_FINGERS_3[orderIndex]);
                       else Assert(false, 'Fingers ordering search algorithm error');
                    end;
                 2: case firstFingerBarreOrder of
                       0: for orderIndex := 0 to High(F1BARRE_PLUS_ONE_FINGER_1) do
                             MaybeAddItem(F1BARRE_PLUS_ONE_FINGER_1[orderIndex]);
                       1: for orderIndex := 0 to High(F1BARRE_PLUS_ONE_FINGER_2) do
                             MaybeAddItem(F1BARRE_PLUS_ONE_FINGER_2[orderIndex]);
                       else Assert(false, 'Fingers ordering search algorithm error');
                    end;
                 1: for orderIndex := 0 to High(F1BARRE_WITHOUT_OTHER_FINGER) do
                       MaybeAddItem(F1BARRE_WITHOUT_OTHER_FINGER[orderIndex]);
              end;
           end
      else // Regular
           begin
              case voicing.fingerCount of
                 4: for orderIndex := 0 to High(FOUR_FINGERS_ORDER_TABLE) do
                       MaybeAddItem(FOUR_FINGERS_ORDER_TABLE[orderIndex]);
                 3: for orderIndex := 0 to High(THREE_FINGERS_ORDER_TABLE) do
                       MaybeAddItem(THREE_FINGERS_ORDER_TABLE[orderIndex]);
                 2: for orderIndex := 0 to High(TWO_FINGERS_ORDER_TABLE) do
                       MaybeAddItem(TWO_FINGERS_ORDER_TABLE[orderIndex]);
                 1: for orderIndex := 0 to High(ONE_FINGER_ORDER_TABLE) do
                       MaybeAddItem(ONE_FINGER_ORDER_TABLE[orderIndex]);
              end;
           end;

   // Sort finger orders by score
   if fFingersOrders.count > 0
      then TStringList(fFingersOrders).CustomSort(FingerOrderSort);
end;

procedure TVoicingFingersFinder.Clear;
var
   index : integer;
   item : TFingeringItem;
begin
   for index := 0 to fFingersOrders.count - 1 do
      begin
         Assert(fFingersOrders.Objects[index] is TFingeringItem);
         item := TFingeringItem(fFingersOrders.Objects[index]);
         item.Free;
      end;
   fFingersOrders.Clear;
end;

function TVoicingFingersFinder.GetItemScoreAndCaption(order : TVoicingFingersOrder; item : TFingeringItem; out caption : string) : boolean;

var
   fingerCount : integer;
   barre : boolean;
   str : integer;
   index : integer;
   finger, lastFinger : integer;
   fret, lastFret : integer;
   rootFingerFret : integer;
   permittedFingersBelow, permittedFingersAbove : TFingers;
   fingers : array [0..3] of integer;
   strs : array [0..3] of integer;

   finger1, finger2 : integer;
   str1, str2 : integer;
   fret1, fret2 : integer;
   distanceScore : integer;

   distance, minDistance, maxDistance, optimalDistance : extended;

   deltaFret, deltaFinger : integer;

   barrePositionUsed : boolean;

   function AreFingerInverted(finger1, finger2 : integer) : boolean;
   var
      index : integer;
   begin
      result := false;
      for index := 0 to 3 do
         begin
                 if order[index] = finger1
                    then begin
                            result := false;
                            Exit;
                         end
            else if order[index] = finger2
                    then begin
                            result := true;
                            Exit;
                         end;
         end;
   end;

   function GetDistanceScore(distance, minDistance, maxDistance, optimalDistance : extended) : integer;
   begin
      Assert(maxDistance - minDistance <> 0);
      result := 0;
           if (distance >= minDistance) and (distance <= optimalDistance)
              then // Between minimum and optimum
                   result := 0
      else if distance < minDistance
              then // Below min distance
                   result := Round((minDistance - distance) * 100 / ((maxDistance - minDistance)))
      else if distance > optimalDistance
              then // Above optimalDistance distance
                   result := Round(((distance - minDistance) * 100) / (maxDistance - minDistance))
      else         Assert(false, 'Algorithm error');
   end;

begin
   // Inits
   fingerCount := voicing.fingerCount;
   barre := voicing.firstFingerBarreExtent > 0;
   for index := 0 to 3 do
      begin
         fingers[index] := -1;
         strs[index] := -1;
      end;
   result := true;
   item.fScore := 0;
   item.fNatural := 0;
   item.fUseFourthFinger := false;
   barrePositionUsed := false;

   // Retrieve the fret for each finger
   // eliminate impossible positions
   str := 0;
   lastFinger := 0;
   lastFret := 0;
   rootFingerFret := 0;

   for index := 0 to fingerCount - 1 do
      begin
         // Retrieve the string for the position
         if barre
            then // Barre
                 begin
                    // Find the position string
                    while (str <= 5) // Ensure we don't exceed the first string
                          and ( (fFormula[str] < 1)
                                or ((fFormula[str] = voicing.minFret) and barrePositionUsed)
                              )
                       do Inc(str);
                    if (not barrePositionUsed)
                       and (fFormula[str] = voicing.minFret)
                       then barrePositionUsed := true;
                 end
            else // No barre
                 begin
                    // Find the position string
                    while (str <= 5) // Ensure we don't exceed the first string
                          and (fFormula[str] < 1)
                       do Inc(str);
                 end;
         if str > 5
            then Exit;

         // Retrieve the fret for the position
         finger := order[index];
         if finger = 4
            then // The fourth finger is used
                 item.fUseFourthFinger := true;
         fret := fFormula[str];

         if lastFinger = 0
            then // Set the root finger fret
                 begin
                    rootFingerFret := fret;
                    permittedFingersBelow := BELOW_FRET_FINGERS[finger - 1];
                    permittedFingersAbove := ABOVE_FRET_FINGERS[finger - 1];
                 end
            else // Check if the fret is permitted for this finger
                 begin
                    if ((fret < rootFingerFret)
                        and (not (finger in permittedFingersBelow)))
                       or
                       ((fret > rootFingerFret)
                        and (not (finger in permittedFingersAbove)))
                          then begin
                                  result := false;
                                  Exit;
                               end;
                 end;

         // Check position validity
         if (fret = lastFret)
            or ((finger > lastFinger) and (fret > lastFret))
            or ((finger < lastFinger) and (fret < lastFret))
            then // Valid position
                 begin
                    // Store finger
                    fingers[finger - 1] := fret;
                    strs[finger - 1] := str + 1;
                    Inc(str);

                    // Store last fret and last finger
                    lastFret := fret;
                    lastFinger := finger;
                 end
            else // Invalid position
                 begin
                    result := false;
                    Exit;
                 end;
       end;

   // Compute finger distances and score
   for finger1 := 1 to 4 do
      begin
         fret1 := fingers[finger1 - 1];
         str1 := strs[finger1 - 1];
         if str1 = -1
            then Continue;

         for finger2 := 1 to 4 do
            begin
               if finger1 < finger2
                  then // Compute the distance
                       begin
                          fret2 := fingers[finger2 - 1];
                          str2 := strs[finger2 - 1];
                          if str2 = -1
                             then Continue;
                          distance := GetFingerDistance(str1, fret1, str2, fret2);

                          // Check finger distances against min/max
                          if AreFingerInverted(finger1, finger2)
                             then begin
                                     minDistance := MIN_FINGER_DISTANCES[finger2 - 1, finger1 - 1];
                                     maxDistance := MAX_FINGER_DISTANCES[finger2 - 1, finger1 - 1];
                                     optimalDistance := OPTIMUM_FINGER_DISTANCES[finger2 - 1, finger1 - 1];

                                     // Inverted fingers are awkward on the same fret
                                     if fret1 = fret2
                                        then Inc(item.fNatural, 10);
                                  end
                             else begin
                                     minDistance := MIN_FINGER_DISTANCES[finger1 - 1, finger2 - 1];
                                     maxDistance := MAX_FINGER_DISTANCES[finger1 - 1, finger2 - 1];
                                     optimalDistance := OPTIMUM_FINGER_DISTANCES[finger1 - 1, finger2 - 1];
                                  end;
                          distanceScore := GetDistanceScore(distance, minDistance, maxDistance, optimalDistance);
                               if distanceScore > 100
                                  then // Impossible position
                                       begin
                                          result := false;
                                          Exit;
                                       end
                          else if distanceScore > 90
                                  then // Almost impossible stretch, big handicap
                                       distanceScore := 200
                          else if distanceScore > 80
                                  then // Big stretch, add a handicap
                                       distanceScore := 100;

                          // Adjust total score
                          Inc(item.fScore, distanceScore);
{
                                          if abs(finger1 - finger2) = 1
                                             then // Neighbor fingers distances are more important
                                                  score := score + distanceScore
                                             else score := score + distanceScore div 2;
}
                       end;
            end;
      end;

   // Additional natural for Delta(fret) > Delta(finger)
   lastFinger := -1;
   lastFret := -1;
   for index := 0 to fingerCount - 1 do
      begin
         finger := order[index];
         fret := fingers[finger - 1];
         if fret <> -1
            then begin
                    if index > 0
                       then begin
                               deltaFret := Abs(lastFret - fret);
                               deltaFinger := Abs(lastFinger - finger);
                                    if deltaFret > deltaFinger
                                       then Inc(item.fNatural, 10)
                               else if deltaFinger > deltaFret + 1
                                       then Inc(item.fNatural, 10)
                            end;
                    lastFret := fret;
                    lastFinger := finger;
                 end;
      end;


   // Set the caption
   caption := '';
   for index := 0 to fingerCount - 1 do
      begin
         if index > 0
            then caption := caption + ' ';
         caption := caption + IntToStr(order[index]);
      end;

   // Adjust item properties for single finger positions
   if voicing.fingerCount = 1
      then begin
              item.fScore := 0;
              item.fNatural := 0;
              item.fUseFourthFinger := false;
           end;
end;


end.