# YOLO Export Formats and Options

Ultralytics YOLO supports exporting to multiple formats, each with specific arguments.

## Available Formats

| Format | Argument (`format=`) | Extension | Uses |
| :--- | :--- | :--- | :--- |
| PyTorch | `torchscript` | `.torchscript` | PyTorch deployment |
| ONNX | `onnx` | `.onnx` | Cross-platform, CPU/GPU, TensorRT base |
| OpenVINO | `openvino` | `_openvino_model/` | Intel CPUs, GPUs, NPUs |
| TensorRT | `engine` | `.engine` | NVIDIA GPUs (fastest inference) |
| CoreML | `coreml` | `.mlpackage` | Apple devices (macOS, iOS) |
| TF SavedModel | `saved_model` | `_saved_model/` | TensorFlow environments |
| TF GraphDef | `pb` | `.pb` | Older TensorFlow deployment |
| TF Lite | `tflite` | `.tflite` | Mobile and Edge devices |
| TF Edge TPU | `edgetpu` | `_edgetpu.tflite` | Google Edge TPU |
| TF.js | `tfjs` | `_web_model/` | Web browsers |
| PaddlePaddle | `paddle` | `_paddle_model/` | PaddlePaddle deployment |
| NCNN | `ncnn` | `_ncnn_model/` | Tencent mobile deployment |

## Common Export Arguments

- `format`: Target format (e.g., `onnx`, `engine`).
- `imgsz`: Image size for export (default `640` or `[640, 640]`).
- `half`: Enable FP16 (half precision) for smaller, faster models (supported by ONNX, TensorRT, OpenVINO, CoreML).
- `int8`: Enable INT8 precision for edge devices (requires calibration data).
- `dynamic`: Allow dynamic input sizes (e.g., dynamic batch size, width, height). Mostly for ONNX and TensorRT.
- `simplify`: Simplify the ONNX graph using `onnxsim` (only for `format=onnx`).
- `opset`: ONNX opset version (e.g., `12`, `13`, `18`).
- `nms`: Inject Non-Maximum Suppression node directly into the model (CoreML, ONNX).
- `workspace`: Workspace size in GB for TensorRT (e.g., `workspace=4`).
- `device`: Device to use for export (`cpu`, `0`, `0,1`).
- `batch`: Batch size to fix during export. Can be combined with `dynamic=False` to fix specific batch sizes like `batch=4`.
