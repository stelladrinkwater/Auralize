# pip install numpy scipy python-osc soundfile
# use: python spatialvis.py --file ambitest.wav --ip 127.0.0.1 --port 7000 --points 50

import numpy as np
from scipy.io import wavfile
import scipy.signal as signal
from pythonosc import udp_client
import time
import soundfile as sf
import argparse
import os

def process_ambisonic_file(file_path, ip="127.0.0.1", port=7001, num_points=50):
    # setup osc sender
    osc = udp_client.SimpleUDPClient(ip, port)
    print(f"sending osc messages to {ip}:{port}")
    
    # check if file exists
    if not os.path.exists(file_path):
        print(f"error: file '{file_path}' not found")
        return
    
    # try to open the file
    print(f"loading file: {file_path}")
    
    try:
        # use soundfile first - this handles many formats
        data, samplerate = sf.read(file_path, always_2d=True)
        print(f"loaded with soundfile: {data.shape}")
        
        # check if we have enough channels for first order ambisonics
        if data.shape[1] < 4:
            print(f"error: expected at least 4 channels but got {data.shape[1]}")
            return
            
        # get separate channels - first 4 are W, X, Y, Z
        w_channel = data[:, 0]
        x_channel = data[:, 1]
        y_channel = data[:, 2]
        z_channel = data[:, 3]
        
    except Exception as e:
        print(f"error with soundfile: {e}")
        print("trying wavfile instead...")
        
        try:
            # fallback to wavfile
            samplerate, data = wavfile.read(file_path)
            print(f"loaded with wavfile: {data.shape}")
            
            # check channels
            if len(data.shape) < 2 or data.shape[1] < 4:
                print(f"error: expected at least 4 channels")
                return
                
            # get channels
            w_channel = data[:, 0]
            x_channel = data[:, 1]
            y_channel = data[:, 2]
            z_channel = data[:, 3]
            
        except Exception as e2:
            print(f"failed to load file: {e2}")
            return
    
    print(f"file loaded with {len(w_channel)} samples at {samplerate}Hz")
    
    # set up parameters for fft
    frame_size = 1024
    hop_size = 512
    
    # create window function - handle both old and new scipy versions
    try:
        window = signal.hann(frame_size)  # newer versions
    except AttributeError:
        window = signal.windows.hann(frame_size)  # older versions
    
    print("created window function")
    
    # normalize data if needed
    max_val = max(np.max(np.abs(w_channel)), np.max(np.abs(x_channel)), 
                 np.max(np.abs(y_channel)), np.max(np.abs(z_channel)))
    
    if max_val > 1.0:
        print(f"normalizing data (max value was {max_val})")
        w_channel = w_channel / max_val
        x_channel = x_channel / max_val
        y_channel = y_channel / max_val
        z_channel = z_channel / max_val
    
    # process the file frame by frame
    num_frames = (len(w_channel) - frame_size) // hop_size
    print(f"processing {num_frames} frames...")
    
    for frame in range(num_frames):
        # get start and end of current frame
        start = frame * hop_size
        end = start + frame_size
        
        # extract the frame and apply window
        w = w_channel[start:end] * window
        x = x_channel[start:end] * window
        y = y_channel[start:end] * window
        z = z_channel[start:end] * window
        
        # do fft
        W = np.fft.rfft(w)
        X = np.fft.rfft(x)
        Y = np.fft.rfft(y)
        Z = np.fft.rfft(z)
        
        # find sound sources
        sound_points = []
        
        # look at each frequency bin
        for bin in range(1, len(W)-1):  # skip dc and nyquist
            # get magnitude of w (energy)
            energy = np.abs(W[bin])
            
            # skip bins with low energy
            if energy < 0.01:
                continue
            
            # skip if W is too close to zero to avoid division problems
            if abs(W[bin].real) < 0.000001:
                continue
                
            # calculate direction vector
            # use real parts for simplicity
            x_dir = X[bin].real / W[bin].real
            y_dir = Y[bin].real / W[bin].real
            z_dir = Z[bin].real / W[bin].real
            
            # check for NaN values
            if np.isnan(x_dir) or np.isnan(y_dir) or np.isnan(z_dir):
                continue
                
            # normalize the vector
            length = np.sqrt(x_dir**2 + y_dir**2 + z_dir**2)
            if length < 0.000001:  # avoid divide by zero
                continue
                
            x_dir /= length
            y_dir /= length
            z_dir /= length
            
            # add to our list
            sound_points.append({
                "x": x_dir,
                "y": y_dir,
                "z": z_dir,
                "energy": energy
            })
        
        # make sure we have some points
        if not sound_points:
            continue
            
        # sort by energy (loudest first)
        sound_points.sort(key=lambda p: p["energy"], reverse=True)
        
        # take only the top ones
        top_points = sound_points[:num_points]
        
        # send osc messages for each point
        for i, point in enumerate(top_points):
            # create message with x, y, z coordinates and energy
            osc.send_message(
                f"/point/{i}", 
                [float(point["x"]), float(point["y"]), float(point["z"]), float(point["energy"])]
            )
            
            # print first point occasionally for debugging
            if i == 0 and frame % 20 == 0:
                print(f"sent point 0: pos=({point['x']:.2f}, {point['y']:.2f}, {point['z']:.2f}), energy={point['energy']:.2f}")
        
        # print progress occasionally
        if frame % 20 == 0:
            print(f"processed frame {frame}/{num_frames} ({frame/num_frames*100:.1f}%)")
            
        # small delay to avoid flooding network
        time.sleep(0.05)
    
    print("finished processing file")

# main part of script
if __name__ == "__main__":
    # set up command line arguments
    parser = argparse.ArgumentParser(description="analyze ambisonic file and send as osc")
    parser.add_argument("--file", required=True, help="path to ambisonic file (.wav or .amb)")
    parser.add_argument("--ip", default="127.0.0.1", help="ip to send osc to")
    parser.add_argument("--port", type=int, default=7000, help="port to send osc to")
    parser.add_argument("--points", type=int, default=50, help="number of points to extract")
    
    # parse arguments
    args = parser.parse_args()
    
    # run the main function
    process_ambisonic_file(args.file, args.ip, args.port, args.points)