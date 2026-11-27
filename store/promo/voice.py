# builds a narration track: python3 voice.py <in.m4a> <out.wav> <lang>
import sys, subprocess
F='node_modules/ffmpeg-static/ffmpeg'
src,out,lang=sys.argv[1:4]
# speech segments (start,end) from silencedetect, grouped per script line; internal pauses shortened
G={'es':[[(1.99,3.73)],[(5.53,8.70)],[(10.55,11.16),(12.29,13.68)],[(15.79,17.79)],[(19.43,20.96)],[(22.78,24.53)],[(26.39,27.65),(28.44,30.07)],[(32.09,38.35),(39.09,40.78)]],
   'en':[[(1.13,2.21)],[(3.77,6.67)],[(8.68,9.56),(10.49,10.86),(11.67,12.63)],[(14.45,15.70)],[(17.38,18.46)],[(19.81,21.53)],[(23.14,24.30),(25.40,26.93)],[(28.94,33.76),(34.86,36.62)]]}[lang]
AT=[0.5,3.0,7.1,10.3,12.7,14.6,16.7,23.2]; PAUSE=[0,0,.45,0,0,0,.6,.7]; PRE,POST=.12,.18
parts=[];labels=[];n=0
for gi,(grp,at) in enumerate(zip(G,AT)):
    t=at-PRE
    for (a,b) in grp:
        a0,b0=a-PRE,b+POST
        parts.append(f"[0:a]atrim={a0:.3f}:{b0:.3f},asetpts=PTS-STARTPTS,afade=t=in:d=0.04,afade=t=out:st={b0-a0-.06:.3f}:d=0.06,adelay={int(t*1000)}:all=1[p{n}]")
        labels.append(f"[p{n}]"); n+=1
        t+= (b0-a0) + PAUSE[gi] - PRE - POST
fc=";".join(parts)+";"+"".join(labels)+f"amix=inputs={n}:normalize=0,highpass=f=80,afftdn=nf=-30,acompressor=threshold=-20dB:ratio=3:attack=5:release=120,loudnorm=I=-16:TP=-1.5:LRA=7,aresample=48000,apad=whole_dur=33[o]"
subprocess.run([F,'-loglevel','error','-y','-i',src,'-filter_complex',fc,'-map','[o]','-ac','2',out],check=True)
