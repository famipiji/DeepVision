FROM python:3.10-slim

# Install system libraries required for OCR and OpenCV
RUN apt-get update && apt-get install -y \
    libgl1 \
    libglib2.0-0 \
    libgomp1 \
    build-essential \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Install Python dependencies
# We use these specific versions to ensure stability
RUN pip install --no-cache-dir \
    paddlepaddle==2.6.2 \
    paddleocr==2.7.3 \
    pymupdf==1.23.8 \
    opencv-python-headless==4.9.0.80 \
    uvicorn==0.27.1 \
    fastapi==0.109.2 \
    python-multipart==0.0.9 \
    "numpy<2.0.0"

# The code is not copied here because we use LIVE MOUNT in docker-compose
EXPOSE 8000

CMD ["python", "ocr_server.py"]