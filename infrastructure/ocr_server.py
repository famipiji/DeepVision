import os, shutil, cv2, uvicorn, fitz
import numpy as np
from fastapi import FastAPI, File, UploadFile, Query
from paddleocr import PaddleOCR
from PIL import Image, ImageSequence

app = FastAPI()
# This matches your Docker volume mount point
MODEL_DIR = "/root/.paddleocr"

# Supported Format Definitions
MULTI_PAGE_FORMATS = {'.pdf', '.tif', '.tiff'}
SINGLE_PAGE_FORMATS = {'.jpg', '.jpeg', '.png', '.bmp', '.webp'}

# Global engine to save memory
ocr_model = None

def load_models(lang):
    global ocr_model
    if ocr_model is None:
        # Directs Paddle to the exact folder where your volume is mapped
        ocr_model = PaddleOCR(
            use_angle_cls=True, 
            lang=lang, 
            show_log=False, 
            det_model_dir=f"{MODEL_DIR}/whl/det/{lang}",
            rec_model_dir=f"{MODEL_DIR}/whl/rec/{lang}",
            cls_model_dir=f"{MODEL_DIR}/whl/cls"
        )
    return ocr_model

def run_paddle_inference(img_array, page_num, ocr_eng, output_list):
    """Helper to run the actual OCR math and append results."""
    res = ocr_eng.ocr(img_array, cls=True)
    if res and res[0]:
        for line in res[0]:
            output_list.append({
                "page": page_num,
                "text": line[1][0],
                "confidence": round(float(line[1][1]), 4),
                "coordinates": line[0]
            })

@app.post("/ocr")
async def do_ocr(file: UploadFile = File(...), lang: str = Query("en")):
    file_ext = os.path.splitext(file.filename)[1].lower()
    
    # 1. Cleverly ignore non-processable files
    if file_ext not in MULTI_PAGE_FORMATS and file_ext not in SINGLE_PAGE_FORMATS:
        return {
            "status": "ignored", 
            "message": f"Format {file_ext} is not compatible for OCR.", 
            "total_items": 0, 
            "data": []
        }

    temp_path = f"temp_{file.filename}"
    with open(temp_path, "wb") as buffer:
        shutil.copyfileobj(file.file, buffer)
    
    try:
        ocr = load_models(lang)
        final_output = []

        # 2. Handle Multi-page PDF
        if file_ext == '.pdf':
            doc = fitz.open(temp_path)
            for page_index, page in enumerate(doc):
                pix = page.get_pixmap(matrix=fitz.Matrix(3, 3)) # 300 DPI equivalent
                img = np.frombuffer(pix.samples, dtype=np.uint8).reshape(pix.h, pix.w, pix.n)
                img = cv2.cvtColor(img, cv2.COLOR_RGB2BGR)
                run_paddle_inference(img, page_index + 1, ocr, final_output)
            doc.close()

        # 3. Handle Multi-page TIFF
        elif file_ext in {'.tif', '.tiff'}:
            with Image.open(temp_path) as img_stack:
                for i, page in enumerate(ImageSequence.Iterator(img_stack)):
                    # Convert to RGB array for OpenCV compatibility
                    page_conv = page.convert('RGB')
                    img_array = cv2.cvtColor(np.array(page_conv), cv2.COLOR_RGB2BGR)
                    run_paddle_inference(img_array, i + 1, ocr, final_output)

        # 4. Handle Standard Images (JPG, PNG, etc.)
        else:
            img = cv2.imread(temp_path)
            if img is not None:
                run_paddle_inference(img, 1, ocr, final_output)
        
        return {
            "status": "completed", 
            "total_items": len(final_output), 
            "data": final_output
        }

    except Exception as e:
        return {"status": "error", "message": str(e)}
    finally:
        if os.path.exists(temp_path):
            os.remove(temp_path)

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8000)