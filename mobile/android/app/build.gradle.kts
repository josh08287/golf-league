plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
    id("com.google.gms.google-services")
    id("dev.flutter.flutter-gradle-plugin")
}

android {
    namespace = "com.golfleague.app"
    compileSdk = 36
    ndkVersion = "28.2.13676358"

    defaultConfig {
        applicationId = "com.golfleague.app"
        minSdk = 24
        targetSdk = 36
        versionCode = 1
        versionName = "1.0"
        manifestPlaceholders["appAuthRedirectScheme"] = "com.golfleague.app"
    }

    signingConfigs {
        getByName("debug") {
            storeFile = file("${System.getProperty("user.home")}/.android/debug.keystore")
            storePassword = "android"
            keyAlias = "androiddebugkey"
            keyPassword = "android"
        }
    }

    buildTypes {
        getByName("release") {
            signingConfig = signingConfigs.getByName("debug")
            isMinifyEnabled = false
            isShrinkResources = false
        }
        getByName("profile") {
            signingConfig = signingConfigs.getByName("debug")
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
        isCoreLibraryDesugaringEnabled = true
    }

}

kotlin {
    compilerOptions {
        jvmTarget = org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_17
    }
}

dependencies {
    coreLibraryDesugaring("com.android.tools:desugar_jdk_libs:2.1.5")
}

// Flutter tool looks for APKs in outputs/flutter-apk/ but AGP 9+ no longer
// copies them there automatically. This task mirrors the APK after assembly.
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
            tasks.named(assembleName) {
                finalizedBy(copyTask)
            }
        }
    }
}
