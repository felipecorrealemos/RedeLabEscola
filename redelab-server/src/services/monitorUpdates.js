const { EventEmitter } = require('node:events');

const monitorUpdates = new EventEmitter();
monitorUpdates.setMaxListeners(0);

function notificarMonitor(motivo, idUsuario = null) {
  monitorUpdates.emit('update', {
    motivo,
    id_usuario: Number.isInteger(Number(idUsuario)) ? Number(idUsuario) : null,
  });
}

module.exports = { monitorUpdates, notificarMonitor };
