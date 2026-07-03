# YOLO Export Formats & Arguments

This document summarizes the available export formats and their settings for Ultralytics YOLO models.

## Available Export Formats

| Format | Argument (`format=...`) | Output Extension | Engine/Platform | Hardware |
|---|---|---|---|---|
| ONNX | `onnx` | `.onnx` | ONNX Runtime | CPU / GPU |
| TorchScript | `torchscript` | `.torchscript` | PyTorch | CPU / GPU |
| OpenVINO | `openvino` | `_openvino_model` | OpenVINO | Intel CPU |
| TensorRT | `engine` | `.engine` | TensorRT | NVIDIA GPU |
| CoreML | `coreml` | `.mlpackage` | CoreML | Apple (iOS, macOS) |
| TF SavedModel | `saved_model` | `_saved_model` | TensorFlow | CPU / GPU |
| TF GraphDef | `pb` | `.pb` | TensorFlow | CPU / GPU |
| TF Lite | `tflite` | `.tflite` | TF Lite | Mobile / Edge |
| TF Edge TPU | `edgetpu` | `_edgetpu.tflite` | TF Lite | Google Edge TPU |
| TF.js | `tfjs` | `_web_model` | TensorFlow.js | Web Browser |
| PaddlePaddle| `paddle` | `_paddle_model` | PaddlePaddle | CPU / GPU |
| ncnn | `ncnn` | `_ncnn_model` | ncnn | Mobile |

---

## Global Export Arguments

These arguments can be passed via the CLI (e.g., `yolo export model=best.pt format=onnx imgsz=640 half=True`) or via Python API (`model.export(format="onnx", imgsz=640, half=True)`).

| Argument | Type | Default | Description |
|---|---|---|---|
| `format` | `str` | `torchscript` | The target export format (see table above). |
| `imgsz` | `int` or `tuple` | `640` | The input image size. E.g. `640` or `(640, 640)`. |
| `half` | `bool` | `False` | Enables FP16 (Half Precision) quantization. Supported by ONNX, TensorRT, CoreML, OpenVINO. |
| `int8` | `bool` | `False` | Enables INT8 quantization. Often requires calibration data. |
| `dynamic` | `bool` | `False` | Enables dynamic input axes (batch size, image dimensions). Supported by ONNX, TensorRT. |
| `simplify` | `bool` | `False` | Simplifies the ONNX model graph (requires `onnxsim`). |
| `opset` | `int` | `17` | ONNX opset version. Typical values are `11`, `12`, `17`. |
| `workspace`| `float` | `4.0` | TensorRT workspace size in GB. |
| `nms` | `bool` | `False` | Adds an NMS (Non-Maximum Suppression) module to the model output. |
| `optimize` | `bool` | `False` | Specific to TorchScript: optimizes the model for mobile deployments. |
| `keras` | `bool` | `False` | Specific to TensorFlow: uses Keras format. |

## Automation and Usage

The `YoloHelperApp` can automate the creation of these models across multiple configurations by generating and running export CLI commands based on the settings provided in the UI or configuration files.
