(async () => {
  const qs = new URLSearchParams(location.search);
  const token = qs.get("token");
  const sampleRate = Number(qs.get("sr") || 16000);
  const formatTurns = (qs.get("format_turns") || "true") === "true";
  const keytermsJson = qs.get("keyterms") || "[]";

  const led = document.getElementById("led");
  const statusEl = document.getElementById("status");
  const latencyEl = document.getElementById("latency");
  const interimEl = document.getElementById("interim");
  const finalEl = document.getElementById("final");

  let openTs = 0;
  let finalText = "";
  window.getTranscript = () => finalText; // Access pulls this

  const setStatus = (t) => statusEl.textContent = t;
  const setLed = (on) => led.classList.toggle("on", !!on);

  const params = new URLSearchParams({
    sample_rate: String(sampleRate),
    format_turns: String(formatTurns),
    keyterms_prompt: keytermsJson,
    token
  });
  const WS_URL = `wss://streaming.assemblyai.com/v3/ws?${params.toString()}`;

  const ctx = new (window.AudioContext || window.webkitAudioContext)({ sampleRate });
  await ctx.audioWorklet.addModule("worklet.js");

  const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
  const source = ctx.createMediaStreamSource(stream);
  const worklet = new AudioWorkletNode(ctx, "pcm16-writer", {
    processorOptions: { targetSampleRate: sampleRate }
  });
  source.connect(worklet);

  setStatus("Connecting…");
  const ws = new WebSocket(WS_URL);
  ws.binaryType = "arraybuffer";

  // Heartbeat for reliability
  let hb;
  const startHeartbeat = () => {
    hb = setInterval(() => {
      if (ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify({ type: "ping", t: Date.now() }));
      }
    }, 15000);
  };

  ws.onopen = () => {
    openTs = performance.now();
    setStatus("Connected");
    setLed(true);
    startHeartbeat();
    worklet.port.onmessage = (ev) => {
      if (ws.readyState === WebSocket.OPEN) ws.send(ev.data);
    };
  };

  ws.onmessage = (evt) => {
    try {
      const m = JSON.parse(evt.data);
      if (m.message_type === "SessionInfo") {
        const ms = Math.round(performance.now() - openTs);
        latencyEl.textContent = `ready in ${ms} ms`;
      } else if (m.message_type === "PartialTranscript") {
        interimEl.textContent = m.text || "";
      } else if (m.message_type === "FinalTranscript") {
        interimEl.textContent = "";
        const line = (m.text || "").trim();
        if (line) {
          const p = document.createElement("p");
          p.textContent = line;
          finalEl.appendChild(p);
          finalEl.scrollTop = finalEl.scrollHeight;
          finalText += (finalText ? "\r\n" : "") + line;
        }
      }
    } catch (e) {
      console.warn("WS decode error:", e);
    }
  };

  ws.onerror = (e) => {
    setStatus("Error");
    setLed(false);
    console.error("WS error", e);
  };

  ws.onclose = () => {
    setStatus("Closed");
    setLed(false);
    if (hb) clearInterval(hb);
    worklet.port.postMessage({ type: "stop" });
  };

  window.stopStreaming = () => { try { ws.close(); } catch(_){} };
})();
