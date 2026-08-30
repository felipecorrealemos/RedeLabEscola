# Favicon do RedeLab Escola

O template usa temporariamente um favicon vazio para não exibir o ícone padrão do Unity nem inventar uma identidade provisória.

Quando o arquivo oficial estiver disponível:

1. salve-o como `Assets/WebGLTemplates/RedeLabEscola/TemplateData/redelab-favicon.png`;
2. em `Assets/WebGLTemplates/RedeLabEscola/index.html`, troque `href="data:,"` por `href="TemplateData/redelab-favicon.png"`;
3. gere novamente o build WebGL.

Para atualizar também um build já gerado sem rebuild, copie o mesmo arquivo para `Build_WebGL/TemplateData/redelab-favicon.png` e faça a mesma troca em `Build_WebGL/index.html`.
