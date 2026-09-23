import http.server, sys, os
d, port = sys.argv[1], int(sys.argv[2])
class H(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *a, **k): super().__init__(*a, directory=d, **k)
    def end_headers(self):
        p = self.path.split('?')[0]
        if p.endswith('.br'): self.send_header('Content-Encoding', 'br')
        elif p.endswith('.gz'): self.send_header('Content-Encoding', 'gzip')
        self.send_header('Cache-Control', 'no-store')
        super().end_headers()
    def guess_type(self, path):
        if path.endswith('.wasm') or path.endswith('.wasm.unityweb'): return 'application/wasm'
        if path.endswith('.js') or path.endswith('.js.unityweb'): return 'application/javascript'
        return super().guess_type(path)
    def log_message(self, *a): pass
http.server.ThreadingHTTPServer(('127.0.0.1', port), H).serve_forever()
