"""
HandPoseService - FastAPI microservice for guitar hand pose detection
Uses MediaPipe Hands model for detecting hand keypoints from images/video frames
"""

from fastapi import FastAPI, File, UploadFile, HTTPException
from fastapi.responses import JSONResponse
from pydantic import BaseModel, Field
from typing import List, Optional, Literal
import uvicorn
import logging
import numpy as np
from PIL import Image
import io
import cv2

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = FastAPI(
    title="HandPoseService",
    description="Hand pose detection service for guitar playing using MediaPipe",
    version="1.0.0"
)

# Models
class Keypoint(BaseModel):
    """3D keypoint with name and coordinates"""
    name: str
    x: float = Field(..., description="Normalized X coordinate (0-1)")
    y: float = Field(..., description="Normalized Y coordinate (0-1)")
    z: float = Field(..., description="Depth coordinate (relative to wrist)")
    
class Hand(BaseModel):
    """Detected hand with keypoints"""
    id: int = Field(..., description="Hand ID (0 or 1)")
    side: Literal["left", "right"] = Field(..., description="Which hand")
    keypoints: List[Keypoint] = Field(..., description="21 hand landmarks")
    confidence: float = Field(..., description="Detection confidence (0-1)")

class HandPoseResponse(BaseModel):
    """Response from hand pose inference"""
    hands: List[Hand] = Field(default_factory=list, description="Detected hands")
    image_width: int
    image_height: int
    processing_time_ms: float

class GuitarPosition(BaseModel):
    """Guitar string/fret position"""
    string: int = Field(..., ge=1, le=6, description="Guitar string (1-6, high E to low E)")
    fret: int = Field(..., ge=0, le=24, description="Fret number (0 = open)")
    finger: Optional[str] = Field(None, description="Finger name (thumb, index, middle, ring, pinky)")
    confidence: float = Field(..., description="Mapping confidence (0-1)")

class NeckConfig(BaseModel):
    """Guitar neck configuration for mapping"""
    scale_length_mm: float = Field(648.0, description="Scale length in mm (e.g., 648mm for Strat)")
    num_frets: int = Field(22, description="Number of frets")
    string_spacing_mm: float = Field(10.5, description="String spacing at bridge")
    nut_width_mm: float = Field(42.0, description="Nut width")

class GuitarMappingRequest(BaseModel):
    """Request to map hand pose to guitar positions"""
    hand_pose: HandPoseResponse
    neck_config: NeckConfig = Field(default_factory=NeckConfig)
    hand_to_map: Literal["left", "right"] = Field("left", description="Which hand to map (left=fretting, right=picking)")

class GuitarMappingResponse(BaseModel):
    """Response with guitar string/fret positions"""
    positions: List[GuitarPosition] = Field(default_factory=list)
    hand_side: str
    mapping_method: str = Field("geometric", description="Method used for mapping")

# MediaPipe landmark names (21 points per hand)
HAND_LANDMARKS = [
    "WRIST",
    "THUMB_CMC", "THUMB_MCP", "THUMB_IP", "THUMB_TIP",
    "INDEX_FINGER_MCP", "INDEX_FINGER_PIP", "INDEX_FINGER_DIP", "INDEX_FINGER_TIP",
    "MIDDLE_FINGER_MCP", "MIDDLE_FINGER_PIP", "MIDDLE_FINGER_DIP", "MIDDLE_FINGER_TIP",
    "RING_FINGER_MCP", "RING_FINGER_PIP", "RING_FINGER_DIP", "RING_FINGER_TIP",
    "PINKY_MCP", "PINKY_PIP", "PINKY_DIP", "PINKY_TIP"
]

# Placeholder for MediaPipe model (Phase 1: return mock data)
# In Phase 2, we'll load the actual ONNX model
mediapipe_model = None

def load_mediapipe_model():
    """Load MediaPipe Hands ONNX model (Phase 2)"""
    global mediapipe_model
    # TODO Phase 2: Load actual model from Hugging Face
    # from onnxruntime import InferenceSession
    # mediapipe_model = InferenceSession("models/hand_landmark.onnx")
    logger.info("MediaPipe model loading skipped (Phase 1 - using mock data)")

@app.on_event("startup")
async def startup_event():
    """Initialize service on startup"""
    logger.info("Starting HandPoseService...")
    load_mediapipe_model()
    logger.info("HandPoseService ready!")

@app.get("/healthz")
async def health_check():
    """Health check endpoint"""
    return {"status": "healthy", "service": "HandPoseService", "version": "1.0.0"}

@app.get("/metrics")
async def metrics():
    """Prometheus metrics endpoint (placeholder)"""
    return {"requests_total": 0, "inference_time_avg_ms": 0}

@app.post("/v1/handpose/infer", response_model=HandPoseResponse)
async def infer_hand_pose(file: UploadFile = File(...)):
    """
    Detect hand pose from uploaded image
    
    Phase 1: Returns mock data with realistic structure
    Phase 2: Will use actual MediaPipe ONNX model
    """
    try:
        import time
        start_time = time.time()
        
        # Read image
        contents = await file.read()
        image = Image.open(io.BytesIO(contents))
        img_width, img_height = image.size
        
        logger.info(f"Processing image: {img_width}x{img_height}")
        
        # Phase 1: Return mock hand pose data
        # Phase 2: Run actual MediaPipe inference
        mock_hands = [
            Hand(
                id=0,
                side="left",
                keypoints=[
                    Keypoint(name=name, x=0.3 + i*0.02, y=0.5 + i*0.01, z=-0.05 + i*0.002)
                    for i, name in enumerate(HAND_LANDMARKS)
                ],
                confidence=0.95
            )
        ]
        
        processing_time = (time.time() - start_time) * 1000
        
        return HandPoseResponse(
            hands=mock_hands,
            image_width=img_width,
            image_height=img_height,
            processing_time_ms=processing_time
        )
        
    except Exception as e:
        logger.error(f"Error processing image: {e}")
        raise HTTPException(status_code=500, detail=f"Failed to process image: {str(e)}")

@app.post("/v1/handpose/guitar-map", response_model=GuitarMappingResponse)
async def map_to_guitar(request: GuitarMappingRequest):
    """
    Map hand pose keypoints to guitar string/fret positions
    
    Phase 1: Simple geometric mapping
    Phase 3: Advanced mapping with ML
    """
    try:
        # Find the requested hand
        target_hand = None
        for hand in request.hand_pose.hands:
            if hand.side == request.hand_to_map:
                target_hand = hand
                break
        
        if not target_hand:
            return GuitarMappingResponse(
                positions=[],
                hand_side=request.hand_to_map,
                mapping_method="none - hand not detected"
            )
        
        # Phase 1: Simple geometric mapping
        # Map fingertips to approximate fret positions
        positions = []
        
        # Get fingertip keypoints (indices 4, 8, 12, 16, 20)
        fingertip_indices = [4, 8, 12, 16, 20]
        finger_names = ["thumb", "index", "middle", "ring", "pinky"]
        
        for i, (tip_idx, finger_name) in enumerate(zip(fingertip_indices, finger_names)):
            if tip_idx < len(target_hand.keypoints):
                kp = target_hand.keypoints[tip_idx]
                
                # Simple mapping: Y coordinate -> string, X coordinate -> fret
                string_num = int(1 + kp.y * 5)  # Map Y to strings 1-6
                fret_num = int(kp.x * request.neck_config.num_frets)  # Map X to frets
                
                # Clamp values
                string_num = max(1, min(6, string_num))
                fret_num = max(0, min(request.neck_config.num_frets, fret_num))
                
                positions.append(GuitarPosition(
                    string=string_num,
                    fret=fret_num,
                    finger=finger_name,
                    confidence=target_hand.confidence * 0.7  # Lower confidence for mapping
                ))
        
        return GuitarMappingResponse(
            positions=positions,
            hand_side=request.hand_to_map,
            mapping_method="geometric_phase1"
        )
        
    except Exception as e:
        logger.error(f"Error mapping to guitar: {e}")
        raise HTTPException(status_code=500, detail=f"Failed to map hand pose: {str(e)}")

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8080)

