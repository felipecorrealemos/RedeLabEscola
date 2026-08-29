class Presenca {
  constructor() {
    this.conexoesPorUsuario = new Map();
  }

  adicionar(idUsuario, socket) {
    let conexoes = this.conexoesPorUsuario.get(idUsuario);
    if (!conexoes) {
      conexoes = new Set();
      this.conexoesPorUsuario.set(idUsuario, conexoes);
    }
    conexoes.add(socket);
    return conexoes.size;
  }

  remover(idUsuario, socket) {
    const conexoes = this.conexoesPorUsuario.get(idUsuario);
    if (!conexoes) {
      return 0;
    }

    conexoes.delete(socket);
    if (conexoes.size === 0) {
      this.conexoesPorUsuario.delete(idUsuario);
      return 0;
    }
    return conexoes.size;
  }

  resumo() {
    const usuarios = [...this.conexoesPorUsuario.entries()]
      .map(([idUsuario, conexoes]) => ({
        id_usuario: idUsuario,
        conexoes: conexoes.size,
      }))
      .sort((a, b) => a.id_usuario - b.id_usuario);

    return { online: usuarios.length, usuarios };
  }
}

module.exports = { Presenca };
