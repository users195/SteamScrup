// Minimal HTTPS downloader using Node's built-in TLS (bypasses schannel restrictions).
// Usage: node fetch.js <url> <outfile> [proxyHost:proxyPort]
const fs = require('fs');
const https = require('https');
const http = require('http');
const tls = require('tls');
const { URL } = require('url');

const [urlArg, outArg, proxyArg] = process.argv.slice(2);
if (!urlArg || !outArg) {
  console.error('usage: node fetch.js <url> <outfile> [proxyHost:proxyPort]');
  process.exit(2);
}

const proxy = proxyArg
  ? { host: proxyArg.split(':')[0], port: Number(proxyArg.split(':')[1]) }
  : null;

function requestOnce(urlStr, redirectsLeft, cb) {
  const u = new URL(urlStr);
  const onResponse = (res) => {
    if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
      if (redirectsLeft <= 0) return cb(new Error('too many redirects'));
      const next = new URL(res.headers.location, urlStr).toString();
      res.resume();
      console.log('redirect -> ' + next);
      return requestOnce(next, redirectsLeft - 1, cb);
    }
    if (res.statusCode !== 200) {
      return cb(new Error('HTTP ' + res.statusCode + ' for ' + urlStr));
    }
    cb(null, res);
  };

  if (!proxy) {
    https.get({ host: u.hostname, port: u.port || 443, path: u.pathname + u.search,
      headers: { 'user-agent': 'fetch.js', 'accept': '*/*' } }, onResponse)
      .on('error', cb);
    return;
  }

  // Manual CONNECT tunnel so the proxy is used explicitly.
  const connectReq = http.request({
    host: proxy.host, port: proxy.port, method: 'CONNECT',
    path: u.hostname + ':' + (u.port || 443),
    headers: { host: u.hostname + ':' + (u.port || 443) },
  });
  connectReq.on('connect', (res, socket) => {
    if (res.statusCode !== 200) return cb(new Error('CONNECT failed ' + res.statusCode));
    const tlsSocket = tls.connect({ socket, servername: u.hostname }, () => {
      const req = https.request({
        createConnection: () => tlsSocket,
        host: u.hostname, path: u.pathname + u.search,
        headers: { 'user-agent': 'fetch.js', 'accept': '*/*' },
      }, onResponse);
      req.on('error', cb);
      req.end();
    });
    tlsSocket.on('error', cb);
  });
  connectReq.on('error', cb);
  connectReq.end();
}

const tmp = outArg + '.part';
const ws = fs.createWriteStream(tmp);
requestOnce(urlArg, 6, (err, res) => {
  if (err) { console.error('ERR ' + err.message); process.exit(1); }
  const total = Number(res.headers['content-length'] || 0);
  let got = 0, lastPct = -1;
  console.log('STATUS 200, content-length=' + total);
  res.on('data', (c) => {
    got += c.length;
    if (total) {
      const pct = Math.floor((got / total) * 100);
      if (pct >= lastPct + 10) { lastPct = pct; console.log('  ' + pct + '%  ' + (got / 1048576).toFixed(1) + ' MB'); }
    }
  });
  res.pipe(ws);
  ws.on('finish', () => {
    ws.close(() => {
      fs.renameSync(tmp, outArg);
      console.log('DONE bytes=' + got + ' -> ' + outArg);
    });
  });
  ws.on('error', (e) => { console.error('WRITE ERR ' + e.message); process.exit(1); });
});
