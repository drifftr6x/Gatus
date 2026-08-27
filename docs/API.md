# API Reference

## Base URL

```
https://api.example.com/v1
```

## Authentication

```http
Authorization: Bearer <token>
```

## Endpoints

### Identity

- `POST /api/auth/login` - Authenticate user
- `POST /api/auth/refresh` - Refresh token
- `POST /api/auth/logout` - Logout

### Devices

- `GET /api/devices` - List devices
- `POST /api/devices` - Register device
- `GET /api/devices/{id}` - Get device
- `PUT /api/devices/{id}` - Update device
- `DELETE /api/devices/{id}` - Remove device

### Content

- `GET /api/content` - List content
- `POST /api/content` - Upload content
- `GET /api/content/{id}` - Get content
- `DELETE /api/content/{id}` - Delete content

## Error Responses

```json
{
  "error": {
    "code": "ERROR_CODE",
    "message": "Human-readable message",
    "details": {}
  }
}
```
