# 🛠️ YOLO Swiss Army Knife (YSAK)

<p align="center">
  <strong>The ultimate Swiss Army knife for YOLO model deployment, batch exporting, and instant testing</strong>
</p>

<p align="center">
  Tired of manually converting models, writing temporary Python scripts for export, dealing with CLI tools, and checking detections "by eye"? 
  <br>
  <strong>YOLO Swiss Army Knife (YSAK)</strong> is a desktop assistant that automates the routine tasks of optimizing, post-processing, and testing YOLO (Ultralytics) models.
</p>

---

## 🚀 Key Features

### 1. 📊 Run Manager (Model Manager / Runs)
No more wandering through `runs/detect/train*` folders. The application scans your working directory, visualizes training graphs (mAP50, loss, recall) directly in the UI, and keeps all your training runs at your fingertips.

### 2. ⚙️ Advanced Profile-Based Multi-Export
Forget the limitations of exporting with a single checkbox. Configure flexible profiles for batch conversion to **ONNX, OpenVINO, TensorRT**, and other formats:
* Concurrent export in different resolutions (imgsz: `320`, `640`, `1280` with a step of 32).
* Independent precision toggling (**FP32, FP16, INT8**).
* Embedding the **NMS** (Non-Maximum Suppression) node directly into the model graph.
* Graph optimization and simplification using `onnx-simplifier`.

### 3. 🔌 Automated ByteBGR Preprocessing Injection
A specific and powerful feature for C++ / C# production environments. The app can automatically embed a **ByteBGR** preprocessing node into ONNX models (using `NeuroModFlowNet.ONNX` tools). The model then accepts a raw `uint8` byte stream directly from camera feeds, eliminating the need to write normalization code on the client side.

### 4. 🛠️ Post-Export Pipeline Automation
After export, you often need to copy the file to a project directory or upload it to a server. Define a list of `postExports` in the project configuration (`project.ysak`), and YSAK will automatically trigger your scripts (`.ps1`, `.bat`, `.cmd`, or CLI commands), passing the paths to the newly exported models.

### 5. 🎯 Instant Inference Preview
Verify the export results immediately inside the app!
* Choose the generated model, select a test image from the project folder, and see the results instantly.
* Supports various YOLO task types (standard **Detect**, Oriented Bounding Boxes **OBB**, keypoints **Pose**).
* Powered by the high-performance `NeuroModFlowNet.ONNX` library using C# ONNX Runtime.

---

## 🛠️ Requirements & Dependencies

* **OS:** Windows 10/11
* **Runtime:** .NET 10.0 + Avalonia UI (included)
* **External Tools:**
  * Python with `ultralytics` installed in the environment (for training and model exporting).
  * *(Optional)* `NeuroModFlowNet.ONNX.Tools` on system `PATH` or configured via `tools.onnxToolsPath` in `project.ysak` — only needed for ByteBGR injection; inference preview works without it.

---

## 📦 Quick Start

1. Clone the repository:
   ```bash
   git clone https://github.com/KOE73/yolo-swiss-army-knife.git
   ```
2. Open the project in Visual Studio 2022 or JetBrains Rider and build the solution, or build via CLI:
   ```bash
   dotnet build -c Release
   ```
3. Run the compiled executable: `YoloHelperApp.exe`.
4. Point the app to your YOLO workspace directory (the folder containing `images`, `runs`, and config files).

---

## 📝 License

Distributed under the MIT License. See [LICENSE](LICENSE) for more details.
