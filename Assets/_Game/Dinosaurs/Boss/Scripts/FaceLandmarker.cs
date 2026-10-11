using Unity.InferenceEngine;
using UnityEngine;

namespace BowGame
{
    // Face tracking on a still picture: finds the 468 face landmarks (eyes, brows, nose, lips, jaw line...)
    // with the MediaPipe face landmark network (Apache License 2.0), run on the CPU by Unity's Inference Engine.
    //
    // The network looks at a 192x192 square around the face. We start with a square in the middle of the picture
    // and run it a few times, each time re-centring the square on the face it found, so it settles on the face.
    // Everything stays in memory.
    public class FaceLandmarks
    {
        // x,y: position in the picture as UV (0..1, y up). z: depth toward the camera, in the same units as x.
        public Vector3[] points;
        // How sure the network is that this is a face (0..1).
        public float score;
        public bool Found => points != null && score >= 0.5f;
    }

    public static class FaceLandmarker
    {
        const int Size = 192;

        public static FaceLandmarks Find(ModelAsset modelAsset, Texture2D photo, int passes = 3)
        {
            var result = new FaceLandmarks();
            if (modelAsset == null || photo == null || !photo.isReadable) return result;

            Model model = ModelLoader.Load(modelAsset);
            using (var worker = new Worker(model, BackendType.CPU))
            {
                int W = photo.width, H = photo.height;
                // Square box in pixels (y down, like the network's picture coordinates).
                float cx = W * 0.5f, cy = H * 0.47f, side = Mathf.Min(W, H) * 0.85f;
                var data = new float[Size * Size * 3];
                var px = new Vector3[468];

                for (int pass = 0; pass < passes; pass++)
                {
                    float bx = cx - side * 0.5f, by = cy - side * 0.5f;
                    for (int y = 0; y < Size; y++)
                    {
                        float py = by + (y + 0.5f) / Size * side;
                        for (int x = 0; x < Size; x++)
                        {
                            float pxx = bx + (x + 0.5f) / Size * side;
                            Color c = (pxx < 0 || pxx >= W || py < 0 || py >= H)
                                ? Color.black
                                : photo.GetPixelBilinear(pxx / W, 1f - py / H);
                            int i = (y * Size + x) * 3;
                            data[i] = c.r; data[i + 1] = c.g; data[i + 2] = c.b;
                        }
                    }

                    float[] lm, flag;
                    using (var input = new Tensor<float>(new TensorShape(1, Size, Size, 3), data))
                    {
                        worker.Schedule(input);
                        lm = (worker.PeekOutput("conv2d_21") as Tensor<float>).DownloadToArray();
                        flag = (worker.PeekOutput("conv2d_31") as Tensor<float>).DownloadToArray();
                    }
                    result.score = 1f / (1f + Mathf.Exp(-flag[0]));

                    float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                    for (int i = 0; i < 468; i++)
                    {
                        float X = bx + lm[i * 3] / Size * side;
                        float Y = by + lm[i * 3 + 1] / Size * side;
                        float Z = lm[i * 3 + 2] / Size * side;
                        px[i] = new Vector3(X, Y, Z);
                        minX = Mathf.Min(minX, X); maxX = Mathf.Max(maxX, X);
                        minY = Mathf.Min(minY, Y); maxY = Mathf.Max(maxY, Y);
                    }
                    if (result.score < 0.5f && pass > 0) break;
                    cx = (minX + maxX) * 0.5f; cy = (minY + maxY) * 0.5f;
                    side = Mathf.Max(maxX - minX, maxY - minY) * 1.6f;
                }

                result.points = new Vector3[468];
                for (int i = 0; i < 468; i++)
                    result.points[i] = new Vector3(px[i].x / W, 1f - px[i].y / H, -px[i].z / W);
            }
            return result;
        }
    }
}
