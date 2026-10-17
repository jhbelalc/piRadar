// Firma el token (JWT ES256, 20 min) para la API de App Store Connect con la clave .p8 del equipo.
// Uso: swift tools/appstore/asc-jwt.swift <AuthKey.p8> <key-id> <issuer-id>
import CryptoKit
import Foundation

let args = CommandLine.arguments
let pem = try String(contentsOfFile: args[1], encoding: .utf8)
let key = try P256.Signing.PrivateKey(pemRepresentation: pem)

func b64url(_ data: Data) -> String {
    data.base64EncodedString().replacingOccurrences(of: "+", with: "-").replacingOccurrences(of: "/", with: "_")
        .replacingOccurrences(of: "=", with: "")
}

// iat un minuto atrás: si el reloj del Mac va adelantado, Apple rechaza un iat "futuro" con 401 intermitentes.
let now = Int(Date().timeIntervalSince1970) - 60
let header = try JSONSerialization.data(withJSONObject: ["alg": "ES256", "kid": args[2], "typ": "JWT"])
let payload = try JSONSerialization.data(withJSONObject: [
    "iss": args[3], "iat": now, "exp": now + 1200, "aud": "appstoreconnect-v1",
] as [String: Any])
let unsigned = b64url(header) + "." + b64url(payload)
let signature = try key.signature(for: Data(unsigned.utf8)).rawRepresentation
print(unsigned + "." + b64url(signature))
