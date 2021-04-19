'''
File: analysis.py
Project: thermal-profile-autd
Created Date: 19/04/2021
Author: Shun Suzuki
-----
Last Modified: 19/04/2021
Modified By: Shun Suzuki (suzuki@hapis.k.u-tokyo.ac.jp)
-----
Copyright (c) 2021 Hapis Lab. All rights reserved.

'''


import matplotlib.pyplot as plt
import numpy as np
import os
import re
import glob
import pandas as pd


def setup_pyplot():
    plt.rcParams['text.usetex'] = True
    plt.rcParams['axes.grid'] = False
    plt.rcParams['xtick.direction'] = 'in'
    plt.rcParams['ytick.direction'] = 'in'
    plt.rcParams['xtick.major.width'] = 1.0
    plt.rcParams['ytick.major.width'] = 1.0
    plt.rcParams['font.size'] = 16
    plt.rcParams['font.family'] = 'sans-serif'
    plt.rcParams['font.sans-serif'] = 'Arial'
    plt.rcParams["mathtext.fontset"] = 'stixsans'
    plt.rcParams['ps.useafm'] = True
    plt.rcParams['pdf.use14corefonts'] = True
    plt.rcParams['text.latex.preamble'] = r'\usepackage{sfmath}'


def normalized(array):
    max_v = array.max()
    min_v = array.min()
    return (array - min_v) / (max_v - min_v)


def find_nearest(array, value):
    array = np.asarray(array)
    return (np.abs(array - value)).argmin()


def get_40kHz_amp(array, dt):
    N = len(array)
    spectrum = np.fft.rfft(array) / (N / 2)
    magnitude = np.abs(spectrum)
    f = np.fft.rfftfreq(N, dt)
    idx = find_nearest(f, 40e3)
    return magnitude[idx]


def get_data(data_path):
    p = re.compile(r'mic_(\d+).csv')

    sample_rate = 10000000
    mV_per_Pa = 31.6
    dt = 1.0 / sample_rate

    times = []
    for filepath in glob.glob(os.path.join(data_path, '*')):
        m = p.match(filepath.split(os.path.sep)[-1])
        if m is None:
            continue

        time = int(m.group(1))
        times.append(time)

    base_time = np.array(times).min()
    size = len(times)

    times = np.zeros(size)
    sounds = np.zeros(size)
    inputs = np.zeros(size)
    mic_temps = np.zeros(size)
    c = 0
    for filepath in glob.glob(os.path.join(data_path, '*')):
        m = p.match(filepath.split(os.path.sep)[-1])
        if m is None:
            continue

        time = (int(m.group(1)) - base_time) / 10 / 1000

        df = pd.read_csv(filepath_or_buffer=filepath, sep=",")
        sound = df['  A Max [mV]']
        input_sig = df['  B Max [mV]']
        sound = get_40kHz_amp(sound, dt) / mV_per_Pa / np.sqrt(2)
        input_sig = get_40kHz_amp(input_sig, dt)

        df = pd.read_csv(filepath_or_buffer=os.path.join(data_path, 'micTemp_' + m.group(1) + '.csv'), sep=",", header=None)
        mic_temp = df[0][0]

        times[c] = time
        sounds[c] = sound
        inputs[c] = input_sig
        mic_temps[c] = mic_temp
        c += 1

    return times, sounds, inputs, mic_temps


if __name__ == '__main__':
    setup_pyplot()

    times, sounds, inputs, mic_temps = get_data('./2021-04-19_14-08-20')
    fig = plt.figure(figsize=(6, 6), dpi=72)
    ax = fig.add_subplot(111)
    ax.plot(times, sounds)
    ax.plot(times, inputs)
    plt.show()
