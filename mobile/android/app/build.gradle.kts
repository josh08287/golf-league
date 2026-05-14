import java.util.Properties
import java.io.FileInputStream

plugins {
    id("com.android.application")
    id("kotlin-android")
    // The Flutter Gradle Plugin must be applied after the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

val keystoreProperties = Properties()
val keystorePropertiesFile = rootProject.file("key.properties")
if (keystorePropertiesFile.exists()) {
    keystoreProperties.load(FileInputStream(keystorePropertiesFile))
}

android {
    namespace = "com.golfleague.golf_league"
    compileSdk = flutter.compileSdkVersion
    ndkVersion = "28.2.13676358"

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = JavaVersion.VERSION_17.toString()
    }

    defaultConfig {
        applicationId = "com.golfleague.golf_league"
        minSdk = flutter.minSdkVersion
        targetSdk = 34
        versionCode = flutter.versionCode
        versionName = flutter.versionName
        manifestPlaceholders["appAuthRedirectScheme"] = "com.golfleague.app"
    }

    signingConfigs {
        create("release") {
            if (keystorePropertiesFile.exists()) {
                storeFile = file(keystoreProperties["storeFile"] as String)
                storePassword = keystoreProperties["storePassword"] as String
                keyAlias = keystoreProperties["keyAlias"] as String
                keyPassword = keystoreProperties["keyPassword"] as String
            }
        }
    }

    buildTypes {
        release {
            signingConfig = if (keystorePropertiesFile.exists()) {
                signingConfigs.getByName("release")
            } else {
                signingConfigs.getByName("debug")
            }
        }
    }
}

flutter {
    source = "../.."
}

// AGP 9+ no longer copies APKs to flutter-apk/ automatically; do it manually.
afterEvaluate {
    listOf("Debug", "Release", "Profile").forEach { buildType ->
        val assembleName = "assemble$buildType"
        if (tasks.findByName(assembleName) != null) {
            val copyTask = tasks.register<Copy>("copyFlutterApk$buildType") {
                from(layout.buildDirectory.dir("outputs/apk/${buildType.lowercase()}"))
                into(layout.buildDirectory.dir("outputs/flutter-apk"))
                include("*.apk")
                rename { "app-${buildType.lowercase()}.apk" }
            }
            tasks.named(assembleName) { finalizedBy(copyTask) }
        }
    }
}
