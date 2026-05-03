import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hive_flutter/hive_flutter.dart';

import 'app.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  try {
    await Firebase.initializeApp();
  } catch (_) {
    // Firebase unavailable — push notifications won't work but app continues.
  }
  await Hive.initFlutter();
  await Hive.openBox<dynamic>('auth');
  await Hive.openBox<dynamic>('prefs');
  runApp(const ProviderScope(child: GolfLeagueApp()));
}
