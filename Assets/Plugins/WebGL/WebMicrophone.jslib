// Microphone loudness for WebGL builds, where Unity's Microphone class doesn't exist.
// Asks the browser for the mic (it will prompt the player), feeds it through an
// AnalyserNode, and hands back the RMS level of the latest samples. See MicrophoneLevel.cs.
mergeInto(LibraryManager.library, {
  // Starts asking for the microphone. Does nothing if it's already on or being asked for.
  WebMic_Start: function () {
    var mic = Module.WebMic || (Module.WebMic = { state: 0 });
    if (mic.state === 1 || mic.state === 2) return;
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) { mic.state = 3; return; }
    mic.state = 1;
    navigator.mediaDevices.getUserMedia({ audio: true, video: false }).then(function (stream) {
      var Context = window.AudioContext || window.webkitAudioContext;
      var context = new Context();
      var analyser = context.createAnalyser();
      analyser.fftSize = 1024;
      context.createMediaStreamSource(stream).connect(analyser);
      mic.context = context;
      mic.analyser = analyser;
      mic.samples = new Float32Array(analyser.fftSize);
      mic.state = 2;
    }).catch(function () { mic.state = 3; });
  },

  // 0 = off, 1 = asking, 2 = listening, 3 = refused or unavailable.
  WebMic_GetState: function () {
    return Module.WebMic ? Module.WebMic.state : 0;
  },

  // RMS of the latest samples, 0-1.
  WebMic_GetLevel: function () {
    var mic = Module.WebMic;
    if (!mic || mic.state !== 2) return 0;
    if (mic.context.state === 'suspended') mic.context.resume();
    mic.analyser.getFloatTimeDomainData(mic.samples);
    var sum = 0;
    for (var i = 0; i < mic.samples.length; i++) sum += mic.samples[i] * mic.samples[i];
    return Math.sqrt(sum / mic.samples.length);
  },

  // Lets go of the microphone.
  WebMic_Stop: function () {
    var mic = Module.WebMic;
    if (!mic || mic.state !== 2) return;
    if (mic.context) mic.context.close();
    mic.context = null;
    mic.analyser = null;
    mic.state = 0;
  }
});
