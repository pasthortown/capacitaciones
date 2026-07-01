/**
 * Servicio de Usuarios permitidos (lista de usuarios de red que pueden ingresar).
 * Requiere Bearer (policy Admin).
 *
 *   GET    /api/admin/users            -> AdminUserDto[] ({ id, usuarioRed, activo, ultimoLogin })
 *   POST   /api/admin/users            -> 201 AdminUserDto   body: { usuarioRed }
 *   DELETE /api/admin/users/{id}       -> 204
 */
import http from './http.js';

const BASE = '/admin/users';

export function list() {
  return http.get(BASE);
}

export function create(usuarioRed) {
  return http.post(BASE, { usuarioRed });
}

export function remove(id) {
  return http.del(`${BASE}/${id}`);
}

export default { list, create, remove };
