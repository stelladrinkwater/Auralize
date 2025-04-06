using System.Data.Common;
using UnityEditor;
using UnityEngine;
using Exocortex.DSP; // Custom DSP library for FFT
using System.Collections;
using UnityEngine.VFX;

public class UnityPointilismVisualize : MonoBehaviour {
    public GameObject visualizationObject; // prefab to visualize the points
    public VisualEffect vfx; // assign in inspector
    AudioSource audioSource;
    AudioData data; // stores the audio data for the entire processed clip
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    GraphicsBuffer buffer; // buffer for the sound points
    void Start() {
        // StartCoroutine(TestAudioVisualization());
        data = GetSoundPoints();
        Debug.Log($"Sound Frames length: {data.soundFrames.Length}");
        Debug.Log($"Sample Rate: {data.sampleRate}");
        Debug.Log($"Frequency: {audioSource.clip.frequency}");

        buffer = new GraphicsBuffer(
            GraphicsBuffer.Target.Structured,
            data.soundFrames[0].soundPoints.Length,
            sizeof(float) * 4);
    }

    // Update is called once per frame
    void Update() {
        VisualizeUsingVFX();
    }

    public void VisualizeUsingVFX() {
        if (audioSource == null) {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource.isPlaying) {
            float time = audioSource.time;
            SoundFrame frame = GetSoundFrameForTime(time);

            Vector4[] points = new Vector4[frame.soundPoints.Length];
            for (int i = 0; i < frame.soundPoints.Length; i++) {
                SoundPoint point = frame.soundPoints[i];
                if (point.energy < 0.01f) {
                    continue; // skip low energy points
                }
                points[i] = new Vector4(point.x, point.y, point.z, point.energy);
            }

            // process points
            buffer.SetData(points);

            vfx.SetGraphicsBuffer("PointBuffer", buffer);
            vfx.SetInt("PointBufferLength", points.Length);
            vfx.SendEvent("BufferUpdated");
        }
    }

    // Return the hannn window function for the given size
    float[] HanningFunction(int size) {
        float[] window = new float[size];
        for (int i = 0; i < size; i++) {
            window[i] = 0.5f * (1 - Mathf.Cos(2 * Mathf.PI * i / (size)));
        }
        return window;
    }

    public struct AudioData {
        // stores data for the entire audio clip for each frame (aka point in time)
        public SoundFrame[] soundFrames;
        public int numFrames;
        public int sampleRate;
        public int frameSize;
        public int hopSize;

        public AudioData(SoundFrame[] soundFrames, int numFrames, int sampleRate, int frameSize, int hopSize) {
            this.soundFrames = soundFrames;
            this.numFrames = numFrames;
            this.sampleRate = sampleRate;
            this.frameSize = frameSize;
            this.hopSize = hopSize;
        }
    }

    public struct SoundFrame {
        // stores all of the sound points for a single frame

        public SoundPoint[] soundPoints;
    }

    public struct SoundPoint {
        // data structure for a single point in space (w, x, y, z)
        public float energy;
        public float x;
        public float y;
        public float z;

        public SoundPoint(float energy, float x, float y, float z) {
            this.energy = energy;
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

    public SoundFrame GetSoundFrameForTime(float t) {
        int sampleRate = data.sampleRate;
        int hopSize = data.hopSize;
        int numFrames = data.numFrames;
        int frameIndex = Mathf.FloorToInt(t * sampleRate / hopSize);
        if (frameIndex < 0 || frameIndex >= numFrames) {
            Debug.LogError("Frame index out of bounds: " + frameIndex);
            return default;
        }
        SoundFrame frame = data.soundFrames[frameIndex];
        if (frame.soundPoints == null || frame.soundPoints.Length == 0) {
            Debug.LogError("No sound points found for frame index: " + frameIndex);
            return default;
        }
        return frame;
    }

    // Return an array of points and energy values for the entire audio clip
    public AudioData GetSoundPoints() {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) {
            Debug.LogError("AudioSource component not found on this GameObject.");
            return default;
        }

        // check if there are enough channels for first order ambisonics
        if (audioSource.clip.channels < 4) {
            Debug.LogError("Audio clip must have at least 4 channels for first order ambisonics.");
            return default;
        }

        // get audio data for w, x, y, z channels
        float[] rawAudioData = new float[audioSource.clip.samples * audioSource.clip.channels];
        audioSource.clip.GetData(rawAudioData, 0);

        int channelCount = audioSource.clip.channels;
        int sampleCount = audioSource.clip.samples;
        float[] wChannel = new float[sampleCount];
        float[] xChannel = new float[sampleCount];
        float[] yChannel = new float[sampleCount];
        float[] zChannel = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++) {
            wChannel[i] = rawAudioData[i * channelCount];
            xChannel[i] = rawAudioData[i * channelCount + 1];
            yChannel[i] = rawAudioData[i * channelCount + 2];
            zChannel[i] = rawAudioData[i * channelCount + 3];
        }

        // normalize data if needed
        float maxVal = 0.0f;
        for (int i = 0; i < sampleCount; i++) {
            maxVal = Mathf.Max(maxVal, Mathf.Abs(wChannel[i]));
            maxVal = Mathf.Max(maxVal, Mathf.Abs(xChannel[i]));
            maxVal = Mathf.Max(maxVal, Mathf.Abs(yChannel[i]));
            maxVal = Mathf.Max(maxVal, Mathf.Abs(zChannel[i]));
        }

        if (maxVal > 1.0f) {
            Debug.Log("Normalizing data (max value was " + maxVal + ")");
            for (int i = 0; i < sampleCount; i++) {
                wChannel[i] /= maxVal;
                xChannel[i] /= maxVal;
                yChannel[i] /= maxVal;
                zChannel[i] /= maxVal;
            }
        }

        // set up parameters for fft
        int frameSize = 1024;
        int hopSize = 512;

        // process the file frame by frame
        int numFrames = (wChannel.Length - frameSize) / hopSize;
        Debug.Log($"Processing {numFrames} frames...");

        // create hanning window function
        float[] window = HanningFunction(frameSize);

        // initialize the data array
        AudioData data = new AudioData(
            new SoundFrame[numFrames],  // soundFrames array
            numFrames,                  // total number of frames
            audioSource.clip.frequency, // sample rate
            frameSize,                  // frame size
            hopSize                     // hop size
        );
        data.soundFrames = new SoundFrame[numFrames];

        for (int frame = 0; frame < numFrames; frame++) {
            // get start and end of current frame
            int start = frame * hopSize;
            int end = start + frameSize; // end is exclusive

            // sound source locations with energy as w component
            SoundFrame soundFrame = new SoundFrame() {
                soundPoints = new SoundPoint[frameSize / 2]
            };

            // extract the frame and apply window
            float[] wFrame = new float[frameSize];
            float[] xFrame = new float[frameSize];
            float[] yFrame = new float[frameSize];
            float[] zFrame = new float[frameSize];
            for (int i = 0; i < frameSize; i++) {
                wFrame[i] = wChannel[start + i] * window[i];
                xFrame[i] = xChannel[start + i] * window[i];
                yFrame[i] = yChannel[start + i] * window[i];
                zFrame[i] = zChannel[start + i] * window[i];
            }

            // perform real fft
            Exocortex.DSP.Fourier.RFFT(wFrame, frameSize, FourierDirection.Forward);
            Exocortex.DSP.Fourier.RFFT(xFrame, frameSize, FourierDirection.Forward);
            Exocortex.DSP.Fourier.RFFT(yFrame, frameSize, FourierDirection.Forward);
            Exocortex.DSP.Fourier.RFFT(zFrame, frameSize, FourierDirection.Forward);

            // look at each frequency bin
            for (int bin = 1; bin < frameSize / 2 - 1; bin++) { // skip dc and nyquist
                // get magnitude of W channel
                float energy = Mathf.Abs(wFrame[bin]);

                // skip bins with low energy
                if (energy < 0.01f) {
                    continue;
                }

                // skip if W is too close to zero
                if (Mathf.Abs(wFrame[bin]) < 0.000001f) {
                    continue;
                }

                // calculate direction vector using real parts for simplicity
                float xDir = xFrame[bin] / wFrame[bin];
                float yDir = yFrame[bin] / wFrame[bin];
                float zDir = zFrame[bin] / wFrame[bin];

                // check for NaN values
                if (float.IsNaN(xDir) || float.IsNaN(yDir) || float.IsNaN(zDir)) {
                    continue;
                }

                // normalize the vector
                float length = Mathf.Sqrt(xDir * xDir + yDir * yDir + zDir * zDir);
                if (length < 0.000001f) { // avoid divide by zero
                    continue;
                }

                xDir /= length;
                yDir /= length;
                zDir /= length;

                // store the point in the array
                soundFrame.soundPoints[bin] = new SoundPoint(energy, xDir, yDir, zDir); // store the energy as w component
            }

            // add the frame to the data array
            data.soundFrames[frame] = soundFrame;
        }
        return data;
    }

    void OnDestroy() {
        buffer.Release();
    }
}
