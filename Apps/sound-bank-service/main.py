"""
SoundBankService - FastAPI microservice for AI-powered guitar sound generation
Uses MusicGen for generating guitar sound samples based on symbolic descriptions
"""

from fastapi import FastAPI, HTTPException, BackgroundTasks
from fastapi.responses import JSONResponse, FileResponse
from pydantic import BaseModel, Field
from typing import List, Optional, Literal, Dict
from enum import Enum
import uvicorn
import logging
import uuid
from datetime import datetime
import asyncio
import json
from pathlib import Path

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = FastAPI(
    title="SoundBankService",
    description="AI-powered guitar sound generation using MusicGen",
    version="1.0.0"
)

# Job status enum
class JobStatus(str, Enum):
    QUEUED = "queued"
    PROCESSING = "processing"
    COMPLETED = "completed"
    FAILED = "failed"

# Models
class SoundGenerationRequest(BaseModel):
    """Request to generate a guitar sound sample"""
    instrument: str = Field("electric_guitar", description="Instrument type")
    string: int = Field(..., ge=1, le=6, description="Guitar string (1-6)")
    fret: int = Field(..., ge=0, le=24, description="Fret number")
    velocity: float = Field(0.7, ge=0.0, le=1.0, description="Playing velocity/dynamics")
    technique: List[str] = Field(default_factory=lambda: ["pluck"], description="Playing techniques")
    style_prompt: Optional[str] = Field(None, description="Style description for MusicGen")
    duration_seconds: float = Field(2.0, ge=0.1, le=10.0, description="Sample duration")

class JobResponse(BaseModel):
    """Response when job is created"""
    job_id: str
    status: JobStatus
    estimated_seconds: float
    created_at: str

class SoundSample(BaseModel):
    """Metadata for a generated sound sample"""
    sample_id: str
    instrument: str
    string: int
    fret: int
    velocity: float
    technique: List[str]
    style_prompt: Optional[str]
    duration_seconds: float
    file_path: str
    file_size_bytes: int
    sample_rate: int = 44100
    created_at: str

class JobStatusResponse(BaseModel):
    """Status of a generation job"""
    job_id: str
    status: JobStatus
    progress: float = Field(0.0, ge=0.0, le=1.0)
    status_message: str
    sample: Optional[SoundSample] = None
    error_message: Optional[str] = None
    created_at: str
    updated_at: str

class SearchRequest(BaseModel):
    """Search for existing sound samples"""
    instrument: Optional[str] = None
    string: Optional[int] = None
    fret: Optional[int] = None
    technique: Optional[List[str]] = None
    limit: int = Field(10, ge=1, le=100)

# In-memory storage (Phase 1)
# Phase 2: Replace with MongoDB
jobs: Dict[str, JobStatusResponse] = {}
samples: Dict[str, SoundSample] = {}
sample_storage_path = Path("./samples")
sample_storage_path.mkdir(exist_ok=True)

# Placeholder for MusicGen model (Phase 1: use pre-recorded samples)
musicgen_model = None

def load_musicgen_model():
    """Load MusicGen model (Phase 2)"""
    global musicgen_model
    # TODO Phase 2: Load actual model from Hugging Face
    # from audiocraft.models import MusicGen
    # musicgen_model = MusicGen.get_pretrained('facebook/musicgen-large')
    logger.info("MusicGen model loading skipped (Phase 1 - using mock generation)")

@app.on_event("startup")
async def startup_event():
    """Initialize service on startup"""
    logger.info("Starting SoundBankService...")
    load_musicgen_model()
    logger.info("SoundBankService ready!")

@app.get("/healthz")
async def health_check():
    """Health check endpoint"""
    return {"status": "healthy", "service": "SoundBankService", "version": "1.0.0"}

@app.get("/metrics")
async def metrics():
    """Prometheus metrics endpoint (placeholder)"""
    return {
        "jobs_total": len(jobs),
        "samples_total": len(samples),
        "jobs_queued": sum(1 for j in jobs.values() if j.status == JobStatus.QUEUED),
        "jobs_processing": sum(1 for j in jobs.values() if j.status == JobStatus.PROCESSING),
        "jobs_completed": sum(1 for j in jobs.values() if j.status == JobStatus.COMPLETED),
        "jobs_failed": sum(1 for j in jobs.values() if j.status == JobStatus.FAILED),
    }

@app.post("/v1/sounds/generate", response_model=JobResponse)
async def generate_sound(request: SoundGenerationRequest, background_tasks: BackgroundTasks):
    """
    Queue a sound generation job
    
    Phase 1: Returns mock job, generates placeholder audio
    Phase 2: Uses actual MusicGen model
    """
    try:
        job_id = str(uuid.uuid4())
        now = datetime.utcnow().isoformat()
        
        # Create job
        job = JobStatusResponse(
            job_id=job_id,
            status=JobStatus.QUEUED,
            progress=0.0,
            status_message="Job queued for processing",
            created_at=now,
            updated_at=now
        )
        jobs[job_id] = job
        
        # Queue background task
        background_tasks.add_task(process_generation_job, job_id, request)
        
        logger.info(f"Created generation job {job_id}")
        
        return JobResponse(
            job_id=job_id,
            status=JobStatus.QUEUED,
            estimated_seconds=15.0,  # Phase 1: mock estimate
            created_at=now
        )
        
    except Exception as e:
        logger.error(f"Error creating generation job: {e}")
        raise HTTPException(status_code=500, detail=f"Failed to create job: {str(e)}")

async def process_generation_job(job_id: str, request: SoundGenerationRequest):
    """
    Background task to process sound generation
    
    Phase 1: Creates placeholder audio file
    Phase 2: Uses MusicGen to generate actual audio
    """
    try:
        job = jobs[job_id]
        job.status = JobStatus.PROCESSING
        job.status_message = "Generating sound sample..."
        job.updated_at = datetime.utcnow().isoformat()
        
        # Phase 1: Simulate generation delay
        await asyncio.sleep(2.0)
        
        # Phase 1: Create placeholder audio file
        # Phase 2: Generate with MusicGen
        sample_id = str(uuid.uuid4())
        file_name = f"{sample_id}.wav"
        file_path = sample_storage_path / file_name
        
        # Create a minimal WAV file (silence)
        create_placeholder_audio(file_path, request.duration_seconds)
        
        # Create sample metadata
        sample = SoundSample(
            sample_id=sample_id,
            instrument=request.instrument,
            string=request.string,
            fret=request.fret,
            velocity=request.velocity,
            technique=request.technique,
            style_prompt=request.style_prompt,
            duration_seconds=request.duration_seconds,
            file_path=str(file_path),
            file_size_bytes=file_path.stat().st_size,
            created_at=datetime.utcnow().isoformat()
        )
        samples[sample_id] = sample
        
        # Update job
        job.status = JobStatus.COMPLETED
        job.progress = 1.0
        job.status_message = "Sound sample generated successfully"
        job.sample = sample
        job.updated_at = datetime.utcnow().isoformat()
        
        logger.info(f"Completed generation job {job_id} -> sample {sample_id}")
        
    except Exception as e:
        logger.error(f"Error processing job {job_id}: {e}")
        job = jobs[job_id]
        job.status = JobStatus.FAILED
        job.status_message = "Generation failed"
        job.error_message = str(e)
        job.updated_at = datetime.utcnow().isoformat()

def create_placeholder_audio(file_path: Path, duration: float):
    """Create a placeholder WAV file (Phase 1)"""
    import wave
    import struct
    
    sample_rate = 44100
    num_samples = int(sample_rate * duration)
    
    with wave.open(str(file_path), 'w') as wav_file:
        wav_file.setnchannels(1)  # Mono
        wav_file.setsampwidth(2)  # 16-bit
        wav_file.setframerate(sample_rate)
        
        # Write silence
        for _ in range(num_samples):
            wav_file.writeframes(struct.pack('h', 0))

@app.get("/v1/sounds/jobs/{job_id}", response_model=JobStatusResponse)
async def get_job_status(job_id: str):
    """Get status of a generation job"""
    if job_id not in jobs:
        raise HTTPException(status_code=404, detail=f"Job {job_id} not found")
    
    return jobs[job_id]

@app.get("/v1/sounds/{sample_id}")
async def get_sample_metadata(sample_id: str):
    """Get metadata for a sound sample"""
    if sample_id not in samples:
        raise HTTPException(status_code=404, detail=f"Sample {sample_id} not found")
    
    return samples[sample_id]

@app.get("/v1/sounds/{sample_id}/download")
async def download_sample(sample_id: str):
    """Download the audio file for a sample"""
    if sample_id not in samples:
        raise HTTPException(status_code=404, detail=f"Sample {sample_id} not found")
    
    sample = samples[sample_id]
    file_path = Path(sample.file_path)
    
    if not file_path.exists():
        raise HTTPException(status_code=404, detail=f"Audio file not found")
    
    return FileResponse(
        path=file_path,
        media_type="audio/wav",
        filename=f"{sample_id}.wav"
    )

@app.post("/v1/sounds/search")
async def search_samples(request: SearchRequest):
    """Search for existing sound samples"""
    results = []
    
    for sample in samples.values():
        # Filter by criteria
        if request.instrument and sample.instrument != request.instrument:
            continue
        if request.string is not None and sample.string != request.string:
            continue
        if request.fret is not None and sample.fret != request.fret:
            continue
        if request.technique and not any(t in sample.technique for t in request.technique):
            continue
        
        results.append(sample)
        
        if len(results) >= request.limit:
            break
    
    return {"samples": results, "total": len(results)}

@app.get("/v1/sounds/jobs")
async def list_jobs(status: Optional[JobStatus] = None, limit: int = 10):
    """List generation jobs"""
    filtered_jobs = [
        job for job in jobs.values()
        if status is None or job.status == status
    ]
    
    # Sort by created_at descending
    filtered_jobs.sort(key=lambda j: j.created_at, reverse=True)
    
    return {"jobs": filtered_jobs[:limit], "total": len(filtered_jobs)}

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8080)

