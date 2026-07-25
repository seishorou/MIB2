# MIB Ticket Patch Experimental

Este projeto gera uma DLL BepInEx para localizar e interceptar métodos de tickets do
**Men in Black Arcade**.

## Estado atual

Este primeiro build é propositalmente de **diagnóstico**.

Ele procura em tempo de execução métodos com estes nomes:

- `TicketsWon`
- `DoTickets`
- `SetTickets`
- `MIBData_VendTickets`
- `VendTickets`

Quando algum deles é executado, o plugin registra:

- nome completo da classe e do método;
- argumentos recebidos;
- resultado retornado;
- campos e propriedades cujo nome sugira tickets/payout.

O registro fica normalmente em:

```text
BepInEx\LogOutput.log
```

Este build ainda **não aciona diretamente o dispenser nem cria uma saída no
MAMEHooker**. O log obtido no teste será usado para identificar com segurança o
valor exato que precisa ser enviado ao DemulShooter/MAMEHooker.

## Arquivos obrigatórios

Copie do jogo para a pasta `libs`:

```text
BepInEx\core\BepInEx.dll  -> libs\BepInEx.dll
BepInEx\core\0Harmony.dll -> libs\0Harmony.dll
```

A estrutura deverá ficar assim:

```text
MIB_TicketPatch_GitHub
├── .github
│   └── workflows
│       └── build.yml
├── libs
│   ├── BepInEx.dll
│   └── 0Harmony.dll
├── MIB_TicketPatch.csproj
├── MIB_TicketPatch_Experimental.cs
└── README.md
```

## Compilação no GitHub

1. Crie um repositório privado.
2. Envie todos os arquivos e pastas deste pacote.
3. Coloque `BepInEx.dll` e `0Harmony.dll` dentro de `libs`.
4. Abra a aba **Actions**.
5. Selecione **Compilar MIB Ticket Patch**.
6. Clique em **Run workflow**.
7. Ao terminar, baixe o artifact `MIB_TicketPatch_Experimental`.

## Instalação para teste

Copie:

```text
MIB_TicketPatch_Experimental.dll
```

para:

```text
BepInEx\plugins\
```

Inicie o jogo, jogue uma partida que gere tickets e depois feche o jogo.

Envie o arquivo:

```text
BepInEx\LogOutput.log
```

## Segurança do teste

Faça backup da pasta do jogo antes do teste.

A DLL não altera o `Assembly-CSharp.dll`. O patch é aplicado somente em memória
enquanto o jogo estiver aberto. Para remover, apague a DLL da pasta
`BepInEx\plugins`.
