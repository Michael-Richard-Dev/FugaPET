-- ============================================================
-- 001_bootstrap_admin_desenvolvimento_v1_1_pgadmin.sql
-- Ambiente alvo: DESENVOLVIMENTO
-- Use um hash BCrypt exclusivo para desenvolvimento.
-- Não reutilize senha/hash de outro ambiente.
-- Compatível com pgAdmin Query Tool (SQL puro, sem comandos \if/\quit do psql).
--
-- COMO USAR
--   1. Gere um hash BCrypt work factor 12 na aplicação.
--   2. Substitua apenas <COLE_HASH_BCRYPT_AQUI> abaixo.
--   3. Execute este arquivo após o 000 consolidado.
-- ============================================================

SET search_path TO desenvolvimento;

DO $$
DECLARE
    v_hash text := '<COLE_HASH_BCRYPT_AQUI>';
    v_codigo_admin bigint;
    v_codigo_perfil bigint;
BEGIN
    IF v_hash = '<COLE_HASH_BCRYPT_AQUI>'
       OR length(v_hash) <> 60
       OR v_hash !~ '^\$2[aby]\$[0-9]{2}\$[./A-Za-z0-9]{53}$' THEN
        RAISE EXCEPTION 'Informe um hash BCrypt valido de 60 caracteres em v_hash antes de executar.';
    END IF;

    SELECT codigo_usuario
      INTO v_codigo_admin
      FROM desenvolvimento.usuario
     WHERE lower(trim(login_usuario)) = 'admin'
     ORDER BY codigo_usuario
     LIMIT 1;

    IF v_codigo_admin IS NULL THEN
        INSERT INTO desenvolvimento.usuario (
            nome_usuario,
            login_usuario,
            email_usuario,
            senha_hash,
            deve_trocar_senha,
            bloqueado_usuario,
            situacao_usuario
        ) VALUES (
            'Administrador Local',
            'admin',
            NULL,
            v_hash,
            true,
            false,
            true
        )
        RETURNING codigo_usuario INTO v_codigo_admin;
    ELSE
        UPDATE desenvolvimento.usuario
           SET senha_hash = v_hash,
               deve_trocar_senha = true,
               bloqueado_usuario = false,
               situacao_usuario = true,
               usuario_atualizado_em = date_trunc('minute', now())
         WHERE codigo_usuario = v_codigo_admin;
    END IF;

    SELECT codigo_perfil_acesso
      INTO v_codigo_perfil
      FROM desenvolvimento.perfil_acesso
     WHERE upper(trim(nome_perfil_acesso)) = 'ADMINISTRADOR'
       AND situacao_perfil_acesso = true
     ORDER BY codigo_perfil_acesso
     LIMIT 1;

    IF v_codigo_perfil IS NULL THEN
        RAISE EXCEPTION 'Perfil Administrador ativo nao encontrado. Verifique a execucao do 000 consolidado.';
    END IF;

    INSERT INTO desenvolvimento.usuario_perfil (
        codigo_usuario,
        codigo_perfil_acesso,
        situacao_usuario_perfil
    )
    SELECT v_codigo_admin, v_codigo_perfil, true
    WHERE NOT EXISTS (
        SELECT 1
          FROM desenvolvimento.usuario_perfil up
         WHERE up.codigo_usuario = v_codigo_admin
           AND up.codigo_perfil_acesso = v_codigo_perfil
    );

    UPDATE desenvolvimento.usuario_perfil
       SET situacao_usuario_perfil = true,
           usuario_perfil_atualizado_em = date_trunc('minute', now())
     WHERE codigo_usuario = v_codigo_admin
       AND codigo_perfil_acesso = v_codigo_perfil
       AND situacao_usuario_perfil = false;
END $$;

SELECT
    u.codigo_usuario,
    u.login_usuario,
    length(u.senha_hash) AS tamanho_hash,
    left(u.senha_hash, 4) AS prefixo_hash,
    u.deve_trocar_senha,
    u.bloqueado_usuario,
    u.situacao_usuario,
    pa.nome_perfil_acesso
FROM desenvolvimento.usuario u
LEFT JOIN desenvolvimento.usuario_perfil up
       ON up.codigo_usuario = u.codigo_usuario
      AND up.situacao_usuario_perfil = true
LEFT JOIN desenvolvimento.perfil_acesso pa
       ON pa.codigo_perfil_acesso = up.codigo_perfil_acesso
WHERE lower(trim(u.login_usuario)) = 'admin';
