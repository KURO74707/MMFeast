import Phaser from 'phaser';
import { STAGE } from './config';
import { GameScene } from './scenes/GameScene';
import { LobbyScene } from './scenes/LobbyScene';
import { App } from './ui/app';
import { mountDebugPanel } from './ui/debug';
import { collectionScreen, upgradesScreen } from './ui/screens/collection';
import { homeScreen } from './ui/screens/home';
import { lobbyScreen } from './ui/screens/lobby';
import { runScreen } from './ui/screens/run';
import { settingsScreen } from './ui/screens/settings';
import './ui/ui.css';

const app = new App(document.getElementById('ui')!);

app.game = new Phaser.Game({
  type: Phaser.AUTO,
  parent: 'stage',
  backgroundColor: '#e6e6e6',
  scale: {
    mode: Phaser.Scale.FIT,
    autoCenter: Phaser.Scale.NO_CENTER,
    width: STAGE.width,
    height: STAGE.height,
  },
  scene: [GameScene, new LobbyScene(app)],
});

for (const screen of [homeScreen(app), settingsScreen(app), collectionScreen(app), upgradesScreen(app), lobbyScreen(app), runScreen(app)]) {
  app.router.register(screen);
}
mountDebugPanel(app);
app.router.go('home');

// Invite links look like ?join=ABCD
const joinCode = new URLSearchParams(location.search).get('join');
if (joinCode && /^[A-Za-z]{4}$/.test(joinCode)) {
  history.replaceState(null, '', location.pathname);
  app.joinSession(joinCode);
}
