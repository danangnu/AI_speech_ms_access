class PCM16Writer extends AudioWorkletProcessor {
  constructor(opts){ super(); this.rate=(opts.processorOptions&&opts.processorOptions.targetSampleRate)||16000; this.buf=[]; this.sum=0; }
  static get parameterDescriptors(){ return []; }
  process(inputs){
    const input = inputs[0];
    if (!input || !input[0] || input[0].length === 0) return true;
    const ch = input[0];
    const inRate = sampleRate, outRate = this.rate, ratio = inRate / outRate;
    const outLen = Math.floor(ch.length / ratio);
    const out = new Int16Array(outLen);
    for (let i=0;i<outLen;i++){
      const idx=i*ratio, i0=Math.floor(idx), i1=Math.min(i0+1, ch.length-1), frac=idx-i0;
      let s=ch[i0]+(ch[i1]-ch[i0])*frac; s=Math.max(-1, Math.min(1, s));
      out[i]=(s*0x7FFF)|0;
    }
    this.buf.push(out); this.sum+=out.length;
    const need=Math.floor(outRate*0.05); // ~50ms
    if (this.sum>=need){
      const joined=new Int16Array(this.sum);
      let off=0; for (const b of this.buf){ joined.set(b, off); off+=b.length; }
      this.buf=[]; this.sum=0;
      this.port.postMessage(joined.buffer, [joined.buffer]);
    }
    return true;
  }
}
registerProcessor("pcm16-writer", PCM16Writer);
