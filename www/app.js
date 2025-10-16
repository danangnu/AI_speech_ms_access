class PCM16Worklet extends AudioWorkletProcessor {
  constructor(options){
    super();
    const o = options.processorOptions||{};
    this.inRate = o.inSampleRate||48000;
    this.outRate = o.outSampleRate||16000;
    this.ratio = this.inRate/this.outRate;
    this.acc = 0;
    this.outBuf = [];
    this.chunkTarget = 1600; // ~100ms
  }
  process(inputs){
    const input = inputs[0]; if(!input||!input[0]) return true;
    const ch0 = input[0];
    for (let i=0;i<ch0.length;i++){
      this.acc += 1;
      if (this.acc >= this.ratio){
        this.acc -= this.ratio;
        this.outBuf.push(ch0[i]);
      }
    }
    if (this.outBuf.length >= this.chunkTarget){
      const f32 = new Float32Array(this.outBuf.splice(0, this.chunkTarget));
      const pcm = new Int16Array(f32.length);
      for (let i=0;i<f32.length;i++){
        let s = Math.max(-1, Math.min(1, f32[i]));
        pcm[i] = s<0 ? s*0x8000 : s*0x7FFF;
      }
      const bytes = new Uint8Array(pcm.buffer);
      this.port.postMessage({type:"chunk", data: bytes.buffer}, [bytes.buffer]);
    }
    return true;
  }
}
registerProcessor("pcm16-worklet", PCM16Worklet);
