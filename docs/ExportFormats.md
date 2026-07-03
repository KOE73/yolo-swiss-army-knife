# YOLO Export Formats and Options

Ultralytics YOLO supports exporting to multiple formats, each with specific arguments.

## Export Formats & Argument Matrix

The table below shows the official supported arguments for each export format as defined in the Ultralytics YOLO documentation:

| Format | `format` Argument | Suffix | CPU | GPU | Supported Arguments |
| :--- | :--- | :--- | :---: | :---: | :--- |
| **PyTorch** | — (native) | `.pt` | ✅ | ✅ | — |
| **TorchScript** | `torchscript` | `.torchscript` | ✅ | ❌ | `imgsz`, `optimize`, `batch` |
| **ONNX** | `onnx` | `.onnx` | ✅ | ✅ | `imgsz`, `half`, `dynamic`, `simplify`, `opset`, `batch` |
| **OpenVINO** | `openvino` | `_openvino_model/` | ✅ | ✅ | `imgsz`, `half`, `int8`, `dynamic`, `batch` |
| **TensorRT** | `engine` | `.engine` | ❌ | ✅ | `imgsz`, `half`, `int8`, `dynamic`, `simplify`, `workspace`, `batch` |
| **CoreML** | `coreml` | `.mlpackage` | ✅ | ❌ | `imgsz`, `half`, `int8`, `nms`, `batch` |
| **TF SavedModel** | `saved_model` | `_saved_model/` | ✅ | ❌ | `imgsz`, `keras`, `int8`, `batch` |
| **TF GraphDef** | `pb` | `.pb` | ✅ | ❌ | `imgsz`, `batch` |
| **TF Lite** | `tflite` | `.tflite` | ✅ | ❌ | `imgsz`, `half`, `int8`, `batch` |
| **TF Edge TPU** | `edgetpu` | `_edgetpu.tflite` | ✅ | ❌ | `imgsz`, `batch` |
| **TF.js** | `tfjs` | `_web_model/` | ✅ | ❌ | `imgsz`, `int8`, `batch` |
| **PaddlePaddle** | `paddle` | `_paddle_model/` | ✅ | ❌ | `imgsz`, `batch` |
| **NCNN** | `ncnn` | `_ncnn_model/` | ✅ | ❌ | `imgsz`, `half`, `int8`, `batch` |

## Detailed Argument Definitions

- **`imgsz`**: Target image size for the exported model (e.g. `imgsz=640` or `imgsz=[640,640]`).
- **`half`**: Enables FP16 (half-precision) quantization.
- **`int8`**: Enables INT8 quantization (requires calibration dataset).
- **`dynamic`**: Enables dynamic input/output axes (highly recommended for ONNX/TensorRT/OpenVINO to support variable image sizes/batches).
- **`simplify`**: Simplifies the ONNX graph using `onnx-simplifier`.
- **`opset`**: ONNX opset version (e.g., `12` or `18`).
- **`nms`**: Integrates Non-Maximum Suppression (NMS) directly into the model graph (supported by CoreML).
- **`workspace`**: Max GPU memory workspace size in GB for TensorRT compilation (e.g. `workspace=4`).
- **`keras`**: Exports to Keras format for TensorFlow models.
- **`optimize`**: Optimizes TorchScript graph.
- **`batch`**: Sets static batch size.
- **`device`**: Target device to use for compilation (e.g. `device=cpu` or `device=0`).
