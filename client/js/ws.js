// WebSocket connection manager
'use strict';

class GameWS {
  constructor(onState, onStatus) {
    this.onState = onState;
    this.onStatus = onStatus;
    this.ws = null;
    this._connect();
  }

  _connect() {
    const url = `ws://${location.host}/ws`;
    this.onStatus('Connecting to ' + url + '…');
    this.ws = new WebSocket(url);

    this.ws.onopen = () => this.onStatus('Connected — waiting for game to boot…');

    this.ws.onmessage = (e) => {
      try {
        const state = JSON.parse(e.data);
        this.onState(state);
        this.onStatus(`Turn ${state.turn} | ${state.pendingAction || 'ok'}`);
      } catch (err) {
        this.onStatus('Parse error: ' + err.message);
      }
    };

    this.ws.onclose = () => {
      this.onStatus('Disconnected — retrying in 3s…');
      setTimeout(() => this._connect(), 3000);
    };

    this.ws.onerror = (e) => this.onStatus('WS error');
  }

  send(obj) {
    if (this.ws?.readyState === WebSocket.OPEN)
      this.ws.send(JSON.stringify(obj));
  }

  move(dir)       { this.send({ type: 'move', param: dir }); }
  endTurn()       { this.send({ type: 'endturn' }); }
  foundCity()     { this.send({ type: 'found' }); }
  keyPress(code)  { this.send({ type: 'key', param: code }); }
}
