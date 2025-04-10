using System.Data.Common;
using UnityEditor;
using UnityEngine;
using Exocortex.DSP; // Custom DSP library for FFT
using System.Collections;
using UnityEngine.VFX;
using System.Net;

public class UnityPointilismVisualize : MonoBehaviour {
    public VisualEffect vfx; // assign in inspector
    AudioSource audioSource;
    AudioData data; // stores the audio data for the entire processed clip
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    GraphicsBuffer mainBuffer; // buffer for the sound points
    GraphicsBuffer freqBuffer;

    // Loudness curve
    public AnimationCurve perceptualLoudnessCurve;

    void Start() {
        data = GetSoundPoints();
        Debug.Log($"Sound Frames length: {data.soundFrames.Length}");
        Debug.Log($"Sample Rate: {data.sampleRate}");
        Debug.Log($"Frequency: {audioSource.clip.frequency}");

        mainBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.Structured,
            data.soundFrames[0].soundPoints.Length,
            sizeof(float) * 4);

        freqBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.Raw,
            data.soundFrames[0].soundPoints.Length,
            sizeof(float) * 1);
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
            float[] frequency = new float[frame.soundPoints.Length];
            for (int i = 0; i < frame.soundPoints.Length; i++) {
                SoundPoint point = frame.soundPoints[i];

                // energy
                float safeFreq = Mathf.Clamp(point.frequency, 50f, 20000f);
                float logFreq = Mathf.Log10(safeFreq);
                float perceptualMultiplier = perceptualLoudnessCurve.Evaluate(logFreq);
                // point.energy has already been clamped between 0.01f and 100f elsewhere
                float adjustedEnergy = point.energy * perceptualMultiplier;

                if (adjustedEnergy > 75f) {
                    Debug.LogWarning("Time " + Time.deltaTime + "Energy: " + adjustedEnergy + " Frequency: " + point.frequency);
                }

                points[i] = new Vector4(point.x, point.y, point.z, adjustedEnergy);
                frequency[i] = point.frequency;
            }

            // process points
            mainBuffer.SetData(points);
            freqBuffer.SetData(frequency);

            vfx.SetGraphicsBuffer("PointBuffer", mainBuffer);
            vfx.SetGraphicsBuffer("FrequencyBuffer", freqBuffer);
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
        // data structure for a single point in space (w, x, y, z, f)
        public float energy;
        public float x;
        public float y;
        public float z;
        public float frequency;

        public SoundPoint(float energy, float x, float y, float z, float frequency) {
            this.energy = energy;
            this.x = x;
            this.y = y;
            this.z = z;
            this.frequency = frequency;
        }
    }

    public SoundFrame GetSoundFrameForTime(float t) {
        // Validate data exists
        if (data.soundFrames == null || data.soundFrames.Length == 0) {
            Debug.LogWarning("No sound frames available");
            return new SoundFrame { soundPoints = new SoundPoint[0] };
        }

        int sampleRate = data.sampleRate;
        int hopSize = data.hopSize;
        int numFrames = data.numFrames;
        int frameIndex = Mathf.FloorToInt(t * sampleRate / hopSize);

        if (frameIndex < 0 || frameIndex >= numFrames) {
            Debug.LogWarning("Frame index out of bounds: " + frameIndex);
            return new SoundFrame { soundPoints = new SoundPoint[0] };
        }
        SoundFrame frame = data.soundFrames[frameIndex];
        if (frame.soundPoints == null || frame.soundPoints.Length == 0) {
            Debug.LogWarning($"No sound points found for frame index: {frameIndex}");
            return new SoundFrame { soundPoints = new SoundPoint[0] };
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
        int samplingRate = audioSource.clip.frequency;
        float[] wChannel = new float[sampleCount];
        float[] xChannel = new float[sampleCount];
        float[] yChannel = new float[sampleCount];
        float[] zChannel = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++) {
            wChannel[i] = rawAudioData[i * channelCount];
            xChannel[i] = rawAudioData[i * channelCount + 3];
            yChannel[i] = rawAudioData[i * channelCount + 1];
            zChannel[i] = rawAudioData[i * channelCount + 2];
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
                float frequency = bin * (samplingRate / frameSize);

                // clamp energy between 0.01f and 100.0f
                if (energy < 0.01f) {
                    energy = 0.01f;
                } else if (energy > 100.0f) {
                    energy = 100.0f;
                }

                // set W very small if W is too close to zero
                if (Mathf.Abs(wFrame[bin]) < 0.000001f) {
                    wFrame[bin] = 0.000001f;
                }

                // calculate direction vector using real parts for simplicity
                float xDir = xFrame[bin] / wFrame[bin];
                float yDir = yFrame[bin] / wFrame[bin];
                float zDir = zFrame[bin] / wFrame[bin];

                // check for NaN values
                if (float.IsNaN(xDir) || float.IsNaN(yDir) || float.IsNaN(zDir)) {
                    Debug.Log($"NaN detected at bin {bin}: xDir={xDir}, yDir={yDir}, zDir={zDir}");
                    continue;
                }

                // normalize the vector
                float length = Mathf.Sqrt(xDir * xDir + yDir * yDir + zDir * zDir);
                if (length < 0.000001f) { // avoid divide by zero (this is hit a lot)
                    continue;
                }

                xDir /= length;
                yDir /= length;
                zDir /= length;

                // store the point in the array
                // mapping coordinates to Unity's system, we get
                soundFrame.soundPoints[bin] = new SoundPoint(
                    energy,
                    yDir,
                    zDir,
                    xDir,
                    frequency
                );
            }

            // add the frame to the data array
            data.soundFrames[frame] = soundFrame;
        }
        return data;
    }

    void OnDestroy() {
        // clean up graphics buffers
        if (mainBuffer != null) {
            mainBuffer.Dispose();
            mainBuffer = null;
        }
        if (freqBuffer != null) {
            freqBuffer.Dispose();
            freqBuffer = null;
        }
    }

    public void UpdateAudioClip(AudioClip newClip) {
        audioSource.clip = newClip;
        data = GetSoundPoints();
        Debug.Log($"Sound Frames length: {data.soundFrames.Length}");
        Debug.Log($"Sample Rate: {data.sampleRate}");
        Debug.Log($"Frequency: {audioSource.clip.frequency}");

        mainBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.Structured,
            data.soundFrames[0].soundPoints.Length,
            sizeof(float) * 4);

        freqBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.Raw,
            data.soundFrames[0].soundPoints.Length,
            sizeof(float) * 1);
    }
}
